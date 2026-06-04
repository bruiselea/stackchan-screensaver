// レンダラに最小限の API を渡す（show/hide の通知だけ）
const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('screensaver', {
  onShow: (cb) => ipcRenderer.on('screensaver:show', cb),
  onHide: (cb) => ipcRenderer.on('screensaver:hide', cb),
})
