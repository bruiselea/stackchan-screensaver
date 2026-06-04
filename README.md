# stackchan-screensaver

放置(無操作)すると全画面にスタックちゃんの顔が出る、Mac 用のスクリーンセーバ風アプリ。
顔は本家 [meganetaaan/m5stack-avatar](https://github.com/meganetaaan/m5stack-avatar) (MIT) の挙動を
参考に、Canvas でプロシージャル描画している（瞬き / 呼吸 / 視線移動）。

## 動かし方 (Mac)

```bash
cd stackchan-screensaver
npm install
npm start
```

3 分(既定)無操作で顔が全画面表示され、マウス/キー入力で消える。

### テストするとき

`main.js` の `IDLE_THRESHOLD_SEC = 180` を `5` などに下げると、すぐ発動して確認しやすい。

## 構成

- `main.js` … Electron メイン。`powerMonitor.getSystemIdleTime()` で無操作秒を監視し、
  しきい値超えで全画面ウィンドウを表示 / 入力で非表示。
- `preload.js` … レンダラへ show/hide 通知を渡す最小ブリッジ。
- `renderer/index.html` … 全画面 Canvas。
- `renderer/face.js` … 顔の描画ロジック。色・サイズは先頭の `FACE` で調整。

## これから

- [ ] 表情バリエ(happy/sleepy 等)の追加
- [ ] 設定 UI(しきい値・色)
- [ ] `.app` 化(electron-builder)してログイン項目に登録 → 常駐
- [ ] (任意) 本家同様の口パク・BMO と喋らせる拡張

## ライセンス

MIT。顔の挙動は m5stack-avatar (Takao Akaki, MIT) を参考に Web 移植。
