// スタックちゃん顔（本家 meganetaaan/m5stack-avatar, MIT を忠実移植）
// 本家 src/Face.cpp / Eye.cpp / Mouth.cpp / ColorPalette.cpp の既定値をそのまま使用。
//
//   仮想画面 320x240（M5 の画面）に本家座標で描き、ウィンドウ全体へ拡大。
//   - 配色: 背景=黒 / 顔=白
//   - 目  : 半径8 の塗り円。右目(90,93) / 左目(230,96)。
//           まばたきは openRatio==0 のとき「幅16×高4 の横線」になる（本家どおりの瞬間方式）
//   - 口  : Mouth(minW=50,maxW=90,minH=4,maxH=60) の塗り矩形。中心(163,148)
//           w = 50 + 40*(1-open),  h = 4 + 56*open,  閉じ時=幅90×高4 の細バー
//   - 視線: gaze(-1..1)*3px のゆらぎ
//   - 呼吸: 口の y に breath*2px

const VW = 320, VH = 240            // 本家の画面サイズ
const PRIMARY = '#ffffff'           // COLOR_PRIMARY = TFT_WHITE
const BG = '#000000'                // COLOR_BACKGROUND = TFT_BLACK

const EYE_R = 8
const EYE_R_POS = { x: 90, y: 93 }  // 本家「右目」 BoundingRect(top=93,left=90)
const EYE_L_POS = { x: 230, y: 96 } // 本家「左目」 BoundingRect(top=96,left=230)
const MOUTH = { x: 163, y: 148, minW: 50, maxW: 90, minH: 4, maxH: 60 }

const canvas = document.getElementById('face')
const ctx = canvas.getContext('2d')

let W = 0, H = 0, scale = 1, offX = 0, offY = 0
function resize() {
  const dpr = window.devicePixelRatio || 1
  W = window.innerWidth
  H = window.innerHeight
  canvas.width = W * dpr
  canvas.height = H * dpr
  // 仮想 320x240 をアスペクト維持で中央にフィット
  scale = Math.min(W / VW, H / VH)
  offX = (W - VW * scale) / 2
  offY = (H - VH * scale) / 2
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0)
}
window.addEventListener('resize', resize)
resize()

// 仮想座標 → 実座標
const sx = (x) => offX + x * scale
const sy = (y) => offY + y * scale
const sr = (r) => r * scale

// ---- 状態 ----
let openRatio = 1            // 両目共通（本家の autoblink も両目同時）
let blinkUntil = 0           // この時刻まで閉じ
let nextBlinkAt = 0
let gaze = { x: 0, y: 0, tx: 0, ty: 0 }
let nextGazeAt = 0
let seed = 12345
function rnd() { // 決定的擬似乱数（Math.random 不使用方針に合わせる）
  seed = (seed * 1103515245 + 12345) & 0x7fffffff
  return seed / 0x7fffffff
}

function scheduleBlink(now) { nextBlinkAt = now + 2000 + rnd() * 4000 }   // 2〜6秒
function scheduleGaze(now)  { nextGazeAt  = now + 2500 + rnd() * 4000 }

function fillCircle(cx, cy, r) {
  ctx.beginPath()
  ctx.arc(sx(cx), sy(cy), sr(r), 0, Math.PI * 2)
  ctx.fill()
}
function fillRect(x, y, w, h) {
  ctx.fillRect(sx(x), sy(y), sr(w), sr(h))
}

function drawEye(pos, gx, gy) {
  ctx.fillStyle = PRIMARY
  if (openRatio > 0) {
    fillCircle(pos.x + gx, pos.y + gy, EYE_R)            // 開: 塗り円
  } else {
    fillRect(pos.x - EYE_R + gx, pos.y - 2 + gy, EYE_R * 2, 4) // 閉: 横線
  }
}

function drawMouth(breath) {
  const open = 0 // 待機は閉じ（将来 lipsync でここを動かす）
  const w = MOUTH.minW + (MOUTH.maxW - MOUTH.minW) * (1 - open)
  const h = MOUTH.minH + (MOUTH.maxH - MOUTH.minH) * open
  ctx.fillStyle = PRIMARY
  fillRect(MOUTH.x - w / 2, MOUTH.y - h / 2 + breath * 2, w, h)
}

let last = 0
function frame(now) {
  if (!last) { last = now; scheduleBlink(now); scheduleGaze(now) }
  const dt = Math.min(0.05, (now - last) / 1000)
  last = now

  // まばたき（本家どおり：一瞬だけ閉じる）
  if (now >= nextBlinkAt && now >= blinkUntil) { blinkUntil = now + 120; scheduleBlink(now) }
  openRatio = (now < blinkUntil) ? 0 : 1

  // 視線（±1 をなめらかに追従 → 描画時 *3px）
  if (now >= nextGazeAt) {
    gaze.tx = (rnd() * 2 - 1)
    gaze.ty = (rnd() * 2 - 1)
    scheduleGaze(now)
  }
  gaze.x += (gaze.tx - gaze.x) * Math.min(1, dt * 4)
  gaze.y += (gaze.ty - gaze.y) * Math.min(1, dt * 4)
  const gx = gaze.x * 3, gy = gaze.y * 3

  // 呼吸（-1..1）
  const breath = Math.sin(now * 0.0016)

  ctx.fillStyle = BG
  ctx.fillRect(0, 0, W, H)
  drawEye(EYE_R_POS, gx, gy)
  drawEye(EYE_L_POS, gx, gy)
  drawMouth(breath)

  requestAnimationFrame(frame)
}
requestAnimationFrame(frame)

if (window.screensaver) {
  window.screensaver.onShow(() => { last = 0 })
  window.screensaver.onHide(() => {})
}
