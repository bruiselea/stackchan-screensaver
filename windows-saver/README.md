# Stackchan Screensaver — Windows版

元のmacOS版の顔・瞬き・呼吸・視線移動をC# / Windows Formsへ移植した、Windows標準のスクリーンセーバーです。
Windows 10 / 11 と .NET Framework 4.8 が対象です。追加のNode.js、Electron、.NET SDK、管理者権限は不要です。

## ダウンロード — v1.0.0-beta.1

- **[Windows版ZIP](https://github.com/bruiselea/stackchan-screensaver/releases/download/windows-v1.0.0-beta.1/StackchanSaver-Windows.zip)**
- [リリース説明とSHA-256チェックサム](https://github.com/bruiselea/stackchan-screensaver/releases/tag/windows-v1.0.0-beta.1)
- [検証内容・結果](https://github.com/bruiselea/stackchan-screensaver/blob/main/windows-saver/docs/TESTING.md)

初回のプレビューリリースです。バイナリは署名されていません。
リリース画面の自動生成された「Source code」はソースコードです。すぐ使う場合は `StackchanSaver-Windows.zip` を選んでください。

### ZIPの内容

| ファイル | 用途 |
| --- | --- |
| `StackchanSaver.exe` | 普通のウィンドウで動くプレビュー |
| `StackchanSaver.scr` | Windowsに登録するスクリーンセーバー |
| `*.config` | .NET Frameworkの起動設定。実行ファイルと一緒に置きます |
| `README.md` | この使い方説明 |
| `verification/` | 自動テスト結果・表情一覧・復帰後の描画画像 |
| `LICENSE` / `THIRD-PARTY-NOTICES.md` | ライセンスとクレジット |

## 最初は普通のウィンドウで試す

ZIPを展開し、**StackchanSaver.exe** をダブルクリックしてください。
普通のウィンドウが開き、遠隔操作中でも試せます。閉じるボタンかEscで終了します。

| キー | 表情 |
| --- | --- |
| N | 通常 |
| H | 嬉しい |
| A | 怒り |
| D | 悲しい |
| S | 眠い |
| Space / 0 | 実際のPC状態に応じた自動切り替え |

アプリはスリープ・画面消灯・ロックを実行しません。電源設定やセーバー設定も変更しません。
プレビュー中もWindowsの通常の電源設定が適用されます。

## スクリーンセーバーとして設定する

1. 展開したフォルダーを、今後も残しておく場所に置きます。
2. **StackchanSaver.scr** を右クリック → **インストール**。Windows 11では「その他のオプションを確認」の中にある場合があります。
3. Windowsの「スクリーン セーバーの設定」で待ち時間などを選び、適用します。

OSの設定画面で「プレビュー」を押すと全画面で起動します。遠隔で試す場合は上記EXEを使ってください。
SCRを単にダブルクリックした場合も、Windowsの関連付けによっては全画面で起動します。
保存場所を移動・削除すると登録先が無効になるので、設定後はその場所に残してください。
解除はWindowsの「スクリーン セーバーの設定」で「なし」または別のセーバーを選びます。
Windowsへの登録操作は自動テストでは行いません。

## 動作

- 優先順位: CPU使用率 > 70% → 怒り、AC接続 → 嬉しい、電池残量 < 20% → 悲しい、表示時間 > 5分 → 眠い、それ以外 → 通常。
- AC接続は充電完了後やバッテリーのないデスクトップでも「嬉しい」です。ACの優先度が高いため、AC接続中は5分を超えても通常は眠くなりません。
- CPUは `GetSystemTimes` の1秒ごとの差分を使用。初回・復帰直後はCPUの基準値を取り直します。64論理CPUを超えるPCでは、Windows APIの制限により呼び出し元のプロセッサーグループの値です。
- バッテリーなし・残量不明を残量0%と誤認しません。
- 温度はWindowsで共通に読めるAPIがないため、今回の移植では取得しません。
- 30fpsで描画。スリープ通知で停止し、復帰通知でタイマー・瞬き・CPU計測を再開します。
- `/s` は接続画面ごとに全画面表示。キー、クリック、ホイール、5pxを超えるマウス移動で全画面を終了します。
- セッション変更や画面構成変更時は全画面セーバーを終了します。次回の無操作起動はWindowsが管理します。
- 常駐プロセス、全体の入力フック、スリープ抑止、独自のパスワード画面はありません。復帰時のサインイン設定はWindowsが管理します。

## 開発・遠隔で安全な自動テスト

ソース版の `windows-saver` で実行:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test -Package
```

Windows付属の .NET Framework C#コンパイラでビルドします。NuGetや外部パッケージのダウンロードはありません。
`dist` にEXE/SCR、`StackchanSaver-Windows.zip` に配布用ZIPを生成します。

自動テストは**表示しない実際のWindowsウィンドウ**を作成し、そこだけにWindowsメッセージを送信します。
OS全体へのブロードキャスト、実際のスリープ、ロック、画面消灯、遠隔切断、全画面表示は行いません。

- CPU・電源情報・時間を模擬し、表情の優先順位としきい値境界を検証。
- `WM_POWERBROADCAST` のスリープ/自動復帰/ユーザー復帰/電源変更をアプリに直接送り、停止・描画再開を確認。
- セッション変更・画面変更・キー・クリックによる終了経路を検証。
- 負の座標を含む2画面を模擬し、片方への入力や画面構成変更で両方のウィンドウが終了することを検証。
- 実際のSCRを `/p HWND` で隠れた親ウィンドウ内に起動し、埋め込み・リサイズ・親終了時の後片付けを検証。
- 実機のCPU/電源APIを読み取り、描画とウィンドウ破棄を反復しGDIリソースの増加を確認。
- `test-results/report.txt` に結果、`faces.png` に全表情、`window-after-resume.png` に復帰後の描画を保存。

`-Test` または `-Package` 付きのビルドではテストを実行し、配布ZIP内の `verification` にもテスト結果と画像を含めます。失敗時はZIPの作成を止めます。

**この代替テストで保証できないもの:** 実際のハードウェアのスリープ復帰、GPUドライバーの復旧、遠隔接続そのものの再接続、複数の実ディスプレイ、Windowsの待機時間経過による起動とサインイン連携。これらは実際の環境での確認が必要です。

## コマンドライン

| 引数 | 動作 |
| --- | --- |
| EXEの引数なし / `--preview` | 安全な通常ウィンドウ |
| SCRの引数なし / `/c` / `/c:HWND` | 説明ダイアログ |
| `/p HWND` / `/p:HWND` | Windows設定の小さいプレビューに埋め込み |
| `/s` | 全画面セーバー（手動での遠隔テストには使用しない） |

不正な引数や無効なプレビュー用ハンドルは、画面を表示せず終了コード2を返します。

## 出典とライセンス

- [元のstackchan-screensaver](https://github.com/bruiselea/stackchan-screensaver): MIT、Copyright (c) 2026 Natsuki Sato (bruiselea)。元のmacOS実装から顔の形状・表情・アニメーションを移植。
- m5stack-avatar: MIT、Copyright (c) 2018 Shinya Ishikawa。付属の `LICENSE` / `THIRD-PARTY-NOTICES.md` を参照。
- 本実装はこのリポジトリのmacOS版からC# / Windows Formsへ移植したものです。スタックちゃんの公式製品ではありません。
- [GetSystemTimes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getsystemtimes)
- [SYSTEM_POWER_STATUS](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-system_power_status)
- [WM_POWERBROADCAST](https://learn.microsoft.com/en-us/windows/win32/power/wm-powerbroadcast)
- [WM_WTSSESSION_CHANGE](https://learn.microsoft.com/en-us/windows/win32/termserv/wm-wtssession-change)
