// プレビュー用の最小ホストアプリ。
// StackchanSaverView を普通の NSWindow に乗せ、startAnimation() で駆動する。
// システム設定で選択し直さなくても、コマンド一発で顔を確認できる。
//   ビルド: swiftc StackchanSaverView.swift preview.swift -framework ScreenSaver -framework Cocoa
//   実行  : ./build.sh preview
import Cocoa
import ScreenSaver

@main
struct Preview {
    static func main() {
        let app = NSApplication.shared
        app.setActivationPolicy(.regular)

        let frame = NSRect(x: 0, y: 0, width: 640, height: 480)
        guard let view = StackchanSaverView(frame: frame, isPreview: false) else {
            fatalError("failed to create view")
        }
        let win = NSWindow(contentRect: frame,
                           styleMask: [.titled, .closable, .resizable, .miniaturizable],
                           backing: .buffered, defer: false)
        win.title = "Stackchan Preview"
        win.contentView = view
        win.center()
        win.makeKeyAndOrderFront(nil)

        view.startAnimation()
        app.activate(ignoringOtherApps: true)
        app.run()
    }
}
