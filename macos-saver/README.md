# macOS 公式スクリーンセーバ (.saver) 版

`StackchanSaverView.swift` は、Electron/Canvas 版 (`renderer/face.js`) と同じ顔を
Apple 公式の `ScreenSaverView` で描く実装。OS がアイドル判定・全画面化・復帰を
やってくれるので、サンドボックスの面倒(オーバーレイ/常駐/入力監視)が発生しない。
→ App Store 提出も視野に入る王道ルート。

## 状況で表情が変わる（許可不要）

`.saver` 内から取れるシステム状態に応じて顔が変化する:

| 状態 | 表情 |
|------|------|
| CPU 負荷 > 70%（最優先） | 怒り顔（眉が吊り上がる） |
| 電源接続（充電中） | 嬉しい顔（◠ の目＋にっこり口） |
| バッテリー < 20% | 眠い顔（半目） |
| それ以外 | 通常 |

IOKit (IOPowerSources) と host_processor_info で取得。マイク/カメラ/位置のような
プライバシー許可は不要なので、スクリーンセーバのまま反応できる。

## Xcode でのビルド手順

1. **Xcode** → File → New → Project → macOS → **Screen Saver** を選択
2. Product Name 例: `StackchanSaver` / Language: **Swift**
3. 生成された `StackchanSaverView.swift`（テンプレのビュークラス）の**中身を本ファイルで置き換える**
   - クラス名はテンプレが付けた名前に合わせるか、`Info.plist` の **`NSPrincipalClass`** を
     `$(PRODUCT_MODULE_NAME).StackchanSaverView` に合わせる（どちらか一致していれば OK）
4. **⌘B** でビルド
5. Products の `StackchanSaver.saver` を右クリック → Finder で表示
6. `.saver` を**ダブルクリック**するとインストールの確認が出る
7. システム設定 → スクリーンセーバ から選択

## 動作確認のコツ

- システム設定のスクリーンセーバ画面の**プレビュー**で即見られる（アイドルを待たなくてよい）
- 直さで全画面確認したいときは:
  `/System/Library/CoreServices/ScreenSaverEngine.app` を起動

## 署名 / 配布（あとで）

- 自前配布: Developer ID で署名 + notarization した `.saver`（または同梱インストーラ）
- App Store: 署名済みアプリに `.saver` を同梱し、ユーザーの `~/Library/Screen Savers` へ
  インストールする形が定番

## ライセンス

MIT。顔の挙動は m5stack-avatar (Takao Akaki, MIT) を参考に移植。
