# stackchan-screensaver

Mac を放置(無操作)すると、スタックちゃん風の顔が全画面に出る **macOS スクリーンセーバ (`.saver`)** です。
顔は [meganetaaan/m5stack-avatar](https://github.com/meganetaaan/m5stack-avatar) (MIT) の
挙動・座標・表情ロジックを参考に Swift (`ScreenSaverView`) へ再実装しています
（瞬き / 呼吸 / 視線移動）。

さらに、**Mac の状態に応じて表情が変わります**（プライバシー許可ダイアログ不要の範囲で取得）。

| 状態（上が優先） | 表情 |
|------|------|
| CPU 負荷が高い / 発熱 | 怒り（目の上を斜め三角でカット） |
| 充電中（電源接続） | 嬉しい（◠ の目） |
| バッテリー < 20% | 悲しい |
| 表示が長い（既定 5 分超） | 眠い（半目） |
| それ以外 | 通常 |

## 動作環境

- **動作確認: macOS 26.3（Apple Silicon）** — 作者の環境のみ。他バージョン/Intel は未検証
- **ビルドターゲット: macOS 11 (Big Sur) 以降**（arm64 / x86_64 ユニバーサル）
- ビルドに Xcode は不要。コマンドラインの `swiftc` のみ

## インストール（Xcode 不要・すべて CLI）

```bash
cd macos-saver
./build.sh open      # ビルド → ~/Library/Screen Savers へ導入 → スクリーンセーバ設定を開く
```

その後、システム設定 → スクリーンセーバ で **StackchanSaver** を選べば本番運用です。

- `./build.sh preview` … NSWindow で即プレビュー（`n`通常 / `h`嬉しい / `a`怒り / `d`悲しい / `s`眠い / `space`自動）
- `./build.sh install` … ビルドして導入のみ
- 仕組み・詳細は [`macos-saver/README.md`](macos-saver/README.md)

## 仕組み（要点）

`swiftc` で arm64 + x86_64 のユニバーサルバイナリを `.saver` バンドルとして組み立て、
ad-hoc 署名まで自動化。アイドル判定・全画面化・復帰は OS（ScreenSaver framework）任せなので、
オーバーレイや常駐の権限が不要です。状態取得は `getloadavg` / `ProcessInfo.thermalState` /
IOKit 電源情報など、許可ダイアログの出ない API を使用。

> **Windows 版は現在作成中**です（Electron ベース・別リポジトリで非公開開発中）。

## 今後

- [ ] **Windows 版（作成中）**
- [ ] 配布版（Developer ID 署名 + notarization）
- [ ] App Store 版（`.saver` を同梱するマネージャアプリ）
- [ ] 設定 UI（しきい値・色）

## ライセンス / クレジット

- 本リポジトリ: **MIT**（[`LICENSE`](LICENSE)）
- 顔の挙動・表情ロジックは **m5stack-avatar** by [@meganetaaan](https://github.com/meganetaaan)
  (Shinya Ishikawa, MIT, Copyright (c) 2018) を参考に再実装しています。
  素晴らしい原作に感謝します 🙏 詳細は [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md)
- 「スタックちゃん / Stack-chan」の名称・キャラクターは [@meganetaaan](https://github.com/meganetaaan)
  氏のプロジェクト [stack-chan](https://github.com/meganetaaan/stack-chan) に由来します。
  本リポジトリは個人的な再実装であり、公式の製品ではありません。

---

# stackchan-screensaver (English)

A **macOS screen saver (`.saver`)** that shows a Stack-chan–style face full-screen
when your Mac is left idle. The face is a reimplementation in Swift (`ScreenSaverView`)
based on the behavior, geometry, and expression logic of
[meganetaaan/m5stack-avatar](https://github.com/meganetaaan/m5stack-avatar) (MIT)
— blinking, breathing, and gaze drift.

It also **changes expression based on your Mac's state** (using only APIs that don't
require a privacy permission prompt).

| State (top has priority) | Expression |
|------|------|
| High CPU load / thermal pressure | Angry (a diagonal cut over the eyes) |
| Charging (AC connected) | Happy (◠-shaped eyes) |
| Battery < 20% | Sad |
| Displayed for a long time (default > 5 min) | Sleepy (half-closed eyes) |
| Otherwise | Neutral |

## Requirements

- **Tested on macOS 26.3 (Apple Silicon)** — author's environment only; other versions / Intel are unverified
- **Build target: macOS 11 (Big Sur) or later** (arm64 / x86_64 universal)
- No Xcode required — builds with the `swiftc` command line only

## Install (no Xcode, all CLI)

```bash
cd macos-saver
./build.sh open      # build → install into ~/Library/Screen Savers → open Screen Saver settings
```

Then pick **StackchanSaver** in System Settings → Screen Saver.

- `./build.sh preview` … instant preview window (`n` neutral / `h` happy / `a` angry / `d` sad / `s` sleepy / `space` auto)
- `./build.sh install` … build and install only
- Details: [`macos-saver/README.md`](macos-saver/README.md)

## How it works

`swiftc` assembles an arm64 + x86_64 universal binary into a `.saver` bundle and
ad-hoc signs it. Idle detection, full-screen, and dismissal are handled by the OS
(ScreenSaver framework), so no overlay or background-agent permissions are needed.
State is read via permission-free APIs such as `getloadavg`,
`ProcessInfo.thermalState`, and IOKit power-source info.

> **A Windows version is currently in development** (Electron-based, in a separate private repo).

## License / Credits

- This repository: **MIT** ([`LICENSE`](LICENSE))
- The face behavior / expression logic is reimplemented based on **m5stack-avatar** by
  [@meganetaaan](https://github.com/meganetaaan) (Shinya Ishikawa, MIT, Copyright (c) 2018).
  Huge thanks to the original work 🙏 See [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).
- The name and character "Stack-chan / スタックちゃん" originate from
  [@meganetaaan](https://github.com/meganetaaan)'s [stack-chan](https://github.com/meganetaaan/stack-chan)
  project. This repository is a personal reimplementation and is **not** an official product.
