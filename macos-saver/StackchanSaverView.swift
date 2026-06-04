// StackchanSaverView.swift
// macOS 公式スクリーンセーバ (.saver) 版。
// Electron/Canvas 版 (renderer/face.js) と同じ座標・挙動を ScreenSaverView へ移植。
// 本家 meganetaaan/m5stack-avatar (MIT) の既定値:
//   仮想画面 320x240 / 目=半径8 塗り円, 右目(90,93) 左目(230,96) / まばたきは横線
//   口=Mouth(50,90,4,60) の塗り矩形, 中心(163,148) / 配色 背景黒・顔白
//   視線 ±3px / 呼吸 口の y に ±2px
//
// Xcode の "Screen Saver" テンプレートで生成されるビュークラスの中身を
// これで置き換える（クラス名と Info.plist の NSPrincipalClass を一致させること）。

import ScreenSaver
import Cocoa

class StackchanSaverView: ScreenSaverView {

    // --- 仮想画面 (M5 の 320x240) ---
    private let vw: CGFloat = 320
    private let vh: CGFloat = 240
    private var cx: CGFloat { vw / 2 } // 160

    // --- 顔パラメータ（本家既定値）---
    private let eyeR: CGFloat = 8
    private let eyeSpread: CGFloat = 70 // 中心から各目まで（160±70 → x=90/230）
    private let eyeY: CGFloat = 94
    private let mouthY: CGFloat = 148
    private let mouthW: CGFloat = 90    // 閉じ時の幅（本家 maxWidth）
    private let mouthH: CGFloat = 4     // 閉じ時の太さ（本家 minHeight）

    // --- アニメーション状態 ---
    private var t: Double = 0
    private var nextBlinkAt: Double = 0
    private var blinkUntil: Double = 0
    private var nextGazeAt: Double = 0
    private var gx: CGFloat = 0, gy: CGFloat = 0     // 現在の視線
    private var tgx: CGFloat = 0, tgy: CGFloat = 0   // 目標の視線

    override init?(frame: NSRect, isPreview: Bool) {
        super.init(frame: frame, isPreview: isPreview)
        animationTimeInterval = 1.0 / 60.0
        scheduleBlink()
        scheduleGaze()
    }

    required init?(coder: NSCoder) {
        super.init(coder: coder)
        animationTimeInterval = 1.0 / 60.0
        scheduleBlink()
        scheduleGaze()
    }

    private func scheduleBlink() { nextBlinkAt = t + 2.0 + Double.random(in: 0...4) } // 2〜6秒
    private func scheduleGaze()  { nextGazeAt  = t + 2.5 + Double.random(in: 0...4) }

    override func animateOneFrame() {
        t += animationTimeInterval

        // まばたき（本家どおり：一瞬だけ閉じる）
        if t >= nextBlinkAt && t >= blinkUntil {
            blinkUntil = t + 0.12
            scheduleBlink()
        }
        // 視線の目標を更新し、なめらかに追従
        if t >= nextGazeAt {
            tgx = CGFloat.random(in: -1...1)
            tgy = CGFloat.random(in: -1...1)
            scheduleGaze()
        }
        let dt = CGFloat(animationTimeInterval)
        gx += (tgx - gx) * min(1, dt * 4)
        gy += (tgy - gy) * min(1, dt * 4)

        setNeedsDisplay(bounds)
    }

    override func draw(_ rect: NSRect) {
        let W = bounds.width, H = bounds.height
        let scale = min(W / vw, H / vh)
        let offX = (W - vw * scale) / 2
        let offY = (H - vh * scale) / 2

        // 仮想座標(左上原点) → ビュー座標(左下原点)
        func px(_ x: CGFloat) -> CGFloat { offX + x * scale }
        func py(_ y: CGFloat) -> CGFloat { H - (offY + y * scale) } // y 反転
        func pr(_ r: CGFloat) -> CGFloat { r * scale }

        // 背景
        NSColor.black.setFill()
        NSBezierPath(rect: bounds).fill()

        NSColor.white.setFill()

        let open = t >= blinkUntil
        let goX = gx * 3, goY = gy * 3

        // 両目
        for ex in [cx - eyeSpread, cx + eyeSpread] {
            let ecx = px(ex + goX)
            let ecy = py(eyeY + goY)
            if open {
                let r = pr(eyeR)
                NSBezierPath(ovalIn: NSRect(x: ecx - r, y: ecy - r, width: r * 2, height: r * 2)).fill()
            } else {
                let w = pr(eyeR * 2), h = pr(4)
                NSBezierPath(rect: NSRect(x: ecx - w / 2, y: ecy - h / 2, width: w, height: h)).fill()
            }
        }

        // 口（呼吸で y が ±2px 揺れる）
        let breath = CGFloat(sin(t * 1.6))
        let mw = pr(mouthW), mh = pr(mouthH)
        let mcx = px(cx)
        let mcy = py(mouthY + breath * 2)
        NSBezierPath(rect: NSRect(x: mcx - mw / 2, y: mcy - mh / 2, width: mw, height: mh)).fill()
    }

    // 設定シートは今は無し
    override var hasConfigureSheet: Bool { false }
    override var configureSheet: NSWindow? { nil }
}
