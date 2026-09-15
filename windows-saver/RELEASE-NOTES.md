# Windows v1.0.0-beta.1

Windows版の最初のプレビューリリースです。macOS版のスタックちゃんの顔・瞬き・呼吸・視線移動を、C# / Windows Formsで移植しました。

## ダウンロードと使い方

1. Assetsの **StackchanSaver-Windows.zip** をダウンロードして展開します。
2. **StackchanSaver.exe** を開くと、普通のウィンドウで試せます。Escで終了できます。
3. スクリーンセーバーに設定する場合は、フォルダーを保存しておく場所に置き、**StackchanSaver.scrを右クリック → インストール**。Windowsの設定画面で待ち時間などを指定してください。

Windows 10 / 11、.NET Framework 4.8が対象です。追加のNode.js・Electron・.NET SDKは不要です。
ZIPにはEXE、SCR、起動設定、日本語の使い方、検証結果と描画画像、ライセンスを含みます。
`SHA256SUMS.txt` はZIPのSHA-256チェックサムです。自動生成の「Source code」は開発用ソースで、配布用ZIPとは別です。

## できること

- CPU使用率 > 70% → 怒り、AC接続 → 嬉しい、電池 < 20% → 悲しい、表示5分超 → 眠い、それ以外 → 通常（この順で優先）。
- プレビューではN/H/A/D/Sで表情を固定、Space / 0で自動に戻せます。
- Windows設定内の小さいプレビュー、複数画面への全画面表示、入力による終了に対応。
- スリープ通知で描画を停止し、復帰通知でタイマーとCPU計測を再開します。
- アプリ自体はスリープ・ロック・遠隔切断を実行せず、電源設定も変更しません。

## テスト結果・未検証の範囲

Windowsビルド26200 / .NET Framework 4.8 / x64環境で **自動テスト15項目に合格**しました。
実際のPCをスリープさせず、非表示のウィンドウに復帰通知を送る方法で検証しています。
表情・描画・模擬2画面の一括終了、実際のSCRの埋め込み起動・リサイズ・終了（5回連続）も確認済みです。

実際のハードウェアのスリープ復帰、RDP再接続、複数の実ディスプレイ、Windowsの無操作起動・サインイン連携、Windows 10 / 32bit / ARM64での動作は未検証です。
Windows版の温度取得は未対応です。バイナリにはコード署名を付けていません。

- [使い方とビルド手順](https://github.com/bruiselea/stackchan-screensaver/blob/main/windows-saver/README.md)
- [検証内容とログ](https://github.com/bruiselea/stackchan-screensaver/blob/main/windows-saver/docs/TESTING.md)

## License / Credits

MIT。元のmacOS実装: Natsuki Sato (bruiselea)。顔の挙動・座標・表情ロジックの原作: m5stack-avatar / Shinya Ishikawa。
スタックちゃんの公式製品ではありません。詳細は同梱のLICENSEとTHIRD-PARTY-NOTICES.mdを参照してください。
