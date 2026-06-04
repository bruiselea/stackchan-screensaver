#!/usr/bin/env bash
# Xcode 不要。swiftc で .saver バンドルをコマンドラインだけで生成する。
#   ./build.sh           → build/StackchanSaver.saver を作る
#   ./build.sh install   → 作って ~/Library/Screen Savers/ にインストール
#   ./build.sh open      → 作ってスクリーンセーバ設定を開く
set -euo pipefail
cd "$(dirname "$0")"

NAME="StackchanSaver"
SRC="StackchanSaverView.swift"
BUNDLE_ID="com.bruiselea.stackchan-screensaver"
OUT="build"
SAVER="$OUT/$NAME.saver"
MACOS="$SAVER/Contents/MacOS"

# プレビュー: ウィンドウで即確認（システム設定での選択不要）
if [ "${1:-}" = "preview" ]; then
  mkdir -p "$OUT"
  echo "==> building preview"
  swiftc "$SRC" preview.swift -framework ScreenSaver -framework Cocoa \
    -target "$(uname -m)-apple-macos11" -o "$OUT/preview"
  echo "==> launching preview (ウィンドウを閉じるか Ctrl+C で終了)"
  exec "$OUT/preview"
fi

rm -rf "$OUT"
mkdir -p "$MACOS"

# arm64 + x86_64 のユニバーサルバイナリを .saver(ロード可能バンドル)として出力
echo "==> compiling (arm64)"
swiftc "$SRC" -emit-library -Xlinker -bundle \
  -framework ScreenSaver -framework Cocoa \
  -target arm64-apple-macos11 -o "$OUT/$NAME.arm64"

echo "==> compiling (x86_64)"
swiftc "$SRC" -emit-library -Xlinker -bundle \
  -framework ScreenSaver -framework Cocoa \
  -target x86_64-apple-macos11 -o "$OUT/$NAME.x86_64"

echo "==> lipo (universal)"
lipo -create -output "$MACOS/$NAME" "$OUT/$NAME.arm64" "$OUT/$NAME.x86_64"
rm -f "$OUT/$NAME.arm64" "$OUT/$NAME.x86_64"

cat > "$SAVER/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleExecutable</key><string>$NAME</string>
  <key>CFBundleIdentifier</key><string>$BUNDLE_ID</string>
  <key>CFBundleName</key><string>$NAME</string>
  <key>CFBundlePackageType</key><string>BNDL</string>
  <key>CFBundleShortVersionString</key><string>0.1.0</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>NSPrincipalClass</key><string>StackchanSaverView</string>
</dict>
</plist>
PLIST

# ローカルで読み込ませるため ad-hoc 署名（無署名だと最近の macOS は弾く）
echo "==> codesign (ad-hoc)"
codesign --force --deep -s - "$SAVER"

echo "built: $SAVER"

case "${1:-}" in
  install|open)
    DEST="$HOME/Library/Screen Savers"
    mkdir -p "$DEST"
    rm -rf "$DEST/$NAME.saver"
    cp -R "$SAVER" "$DEST/"
    echo "installed: $DEST/$NAME.saver"
    if [ "${1:-}" = "open" ]; then
      open "x-apple.systempreferences:com.apple.ScreenSaver-Settings.extension" 2>/dev/null \
        || open -b com.apple.systempreferences
    fi
    ;;
esac
