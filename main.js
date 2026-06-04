// Electron メインプロセス
// アイドル時間を監視し、しきい値を超えたら全画面で顔ウィンドウを表示する。
// 入力（マウス/キー）でアイドルが解除されたら隠す。
const { app, BrowserWindow, powerMonitor, screen } = require('electron')
const path = require('path')

// 無操作がこの秒数を超えたら発動
const IDLE_THRESHOLD_SEC = 180 // 3分（テスト時は 5 などに下げると楽）
const POLL_INTERVAL_MS = 1000

let win = null
let visible = false

function createWindow() {
  const primary = screen.getPrimaryDisplay()
  win = new BrowserWindow({
    width: primary.bounds.width,
    height: primary.bounds.height,
    x: primary.bounds.x,
    y: primary.bounds.y,
    frame: false,
    fullscreen: true,
    show: false,
    alwaysOnTop: true,
    skipTaskbar: true,
    backgroundColor: '#000000',
    webPreferences: {
      contextIsolation: true,
      preload: path.join(__dirname, 'preload.js'),
    },
  })
  win.loadFile(path.join(__dirname, 'renderer', 'index.html'))
  // 表示中は最前面を維持
  win.setVisibleOnAllWorkspaces(true)
}

function show() {
  if (!win || visible) return
  visible = true
  win.showInactive()
  win.setFullScreen(true)
  win.webContents.send('screensaver:show')
}

function hide() {
  if (!win || !visible) return
  visible = false
  win.webContents.send('screensaver:hide')
  win.hide()
}

function startIdleLoop() {
  setInterval(() => {
    const idle = powerMonitor.getSystemIdleTime() // 秒
    if (idle >= IDLE_THRESHOLD_SEC) {
      show()
    } else if (visible) {
      hide()
    }
  }, POLL_INTERVAL_MS)
}

app.whenReady().then(() => {
  createWindow()
  startIdleLoop()
})

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit()
})
