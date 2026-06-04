// スタックちゃん顔（本家 meganetaaan/m5stack-avatar, MIT を忠実移植 + 見た目調整つき）
//
//   仮想画面 320x240（M5 の画面）に本家座標で描き、ウィンドウ全体へ拡大。
//   - 配色: 背景=黒 / 顔=白（本家 ColorPalette 既定）
//   - 目  : 塗り円。まばたきは openRatio==0 のとき横線（本家どおり）
//   - 口  : 塗り矩形。閉じ=幅 mouthW × 太さ mouthH のバー
//   - 視線: gaze(-1..1)*3px / 呼吸: 口の y に breath*2px
//
//   FACE の各値は "c" キーのチューナーで実機を見ながら調整できる。
//   本家の既定値 = eyeR:8, eyeSpread:70, eyeY:94, mouthY:148, mouthW:90, mouthH:4

const VW = 320, VH = 240
const CX = VW / 2 // 160
const PRIMARY = '#ffffff'
const BG = '#000000'

const FACE = {
  faceScale: 1,    // 顔全体の拡大率（1=画面にアスペクト維持でフィット）
  eyeR: 8,         // 目の半径
  eyeSpread: 70,   // 中心から各目までの横距離（本家: 160±70 → x=90/230）
  eyeY: 94,        // 目の高さ（本家: 93,96 のあたり）
  mouthY: 148,     // 口の高さ
  mouthW: 90,      // 口の幅（閉じ時=本家 maxWidth 90）
  mouthH: 4,       // 口の太さ（閉じ時=本家 minHeight 4）
}

const canvas = document.getElementById('face')
const ctx = canvas.getContext('2d')

let W = 0, H = 0, scale = 1, offX = 0, offY = 0
function resize() {
  const dpr = window.devicePixelRatio || 1
  W = window.innerWidth
  H = window.innerHeight
  canvas.width = W * dpr
  canvas.height = H * dpr
  scale = Math.min(W / VW, H / VH) * FACE.faceScale
  offX = (W - VW * scale) / 2
  offY = (H - VH * scale) / 2
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0)
}
window.addEventListener('resize', resize)
resize()

const sx = (x) => offX + x * scale
const sy = (y) => offY + y * scale
const sr = (r) => r * scale

let openRatio = 1
let blinkUntil = 0, nextBlinkAt = 0
let gaze = { x: 0, y: 0, tx: 0, ty: 0 }
let nextGazeAt = 0
let seed = 12345
function rnd() { seed = (seed * 1103515245 + 12345) & 0x7fffffff; return seed / 0x7fffffff }
function scheduleBlink(now) { nextBlinkAt = now + 2000 + rnd() * 4000 }
function scheduleGaze(now)  { nextGazeAt  = now + 2500 + rnd() * 4000 }

function fillCircle(cx, cy, r) { ctx.beginPath(); ctx.arc(sx(cx), sy(cy), sr(r), 0, Math.PI * 2); ctx.fill() }
function fillRect(x, y, w, h) { ctx.fillRect(sx(x), sy(y), sr(w), sr(h)) }

function drawEye(ex, ey, gx, gy) {
  ctx.fillStyle = PRIMARY
  if (openRatio > 0) {
    fillCircle(ex + gx, ey + gy, FACE.eyeR)
  } else {
    fillRect(ex - FACE.eyeR + gx, ey - 2 + gy, FACE.eyeR * 2, 4)
  }
}

function drawMouth(breath) {
  ctx.fillStyle = PRIMARY
  fillRect(CX - FACE.mouthW / 2, FACE.mouthY - FACE.mouthH / 2 + breath * 2, FACE.mouthW, FACE.mouthH)
}

let last = 0
function frame(now) {
  if (!last) { last = now; scheduleBlink(now); scheduleGaze(now) }
  const dt = Math.min(0.05, (now - last) / 1000)
  last = now

  if (now >= nextBlinkAt && now >= blinkUntil) { blinkUntil = now + 120; scheduleBlink(now) }
  openRatio = (now < blinkUntil) ? 0 : 1

  if (now >= nextGazeAt) { gaze.tx = rnd() * 2 - 1; gaze.ty = rnd() * 2 - 1; scheduleGaze(now) }
  gaze.x += (gaze.tx - gaze.x) * Math.min(1, dt * 4)
  gaze.y += (gaze.ty - gaze.y) * Math.min(1, dt * 4)
  const gx = gaze.x * 3, gy = gaze.y * 3
  const breath = Math.sin(now * 0.0016)

  ctx.fillStyle = BG
  ctx.fillRect(0, 0, W, H)
  drawEye(CX - FACE.eyeSpread, FACE.eyeY, gx, gy)
  drawEye(CX + FACE.eyeSpread, FACE.eyeY, gx, gy)
  drawMouth(breath)

  requestAnimationFrame(frame)
}
requestAnimationFrame(frame)

if (window.screensaver) {
  window.screensaver.onShow(() => { last = 0 })
  window.screensaver.onHide(() => {})
}

// ---- 調整パネル（"c" で開閉） ----
const tuner = document.getElementById('tuner')
const KEYS = ['faceScale', 'eyeR', 'eyeSpread', 'eyeY', 'mouthY', 'mouthW', 'mouthH']
function syncTuner() {
  for (const k of KEYS) {
    const slider = document.getElementById('t_' + k)
    const val = document.getElementById('v_' + k)
    if (!slider) continue
    slider.value = FACE[k]
    val.textContent = FACE[k]
    slider.oninput = () => {
      FACE[k] = parseFloat(slider.value)
      val.textContent = FACE[k]
      if (k === 'faceScale') resize()
    }
  }
}
syncTuner()
document.getElementById('t_copy').onclick = () => {
  const out = `FACE = ${JSON.stringify(FACE, null, 2)}`
  console.log(out)
}
window.addEventListener('keydown', (e) => {
  if (e.key === 'c' || e.key === 'C') {
    tuner.classList.toggle('show')
    document.body.style.cursor = tuner.classList.contains('show') ? 'default' : 'none'
  }
})
