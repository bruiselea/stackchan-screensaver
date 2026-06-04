// StackchanSaverView.swift
// macOS 公式スクリーンセーバ (.saver) 版。
// 本家 meganetaaan/m5stack-avatar (MIT) の顔(目=塗り円, 瞬き=横線, 口=矩形)を ScreenSaverView へ移植。
// さらに、許可不要で取れるシステム状態に応じて表情を変える:
//   - CPU 負荷 > 70%        → 怒り顔 (angry)   ※最優先
//   - 電源接続(充電中)      → 嬉しい顔 (happy)
//   - バッテリー < 20%       → 眠い顔 (sleepy)
//   - それ以外               → 通常 (neutral)
// 仮想画面 320x240 / 配色 背景黒・顔白 / 視線±3px / 呼吸 口±2px

import ScreenSaver
import Cocoa
import IOKit.ps
import Darwin

@objc(StackchanSaverView)
class StackchanSaverView: ScreenSaverView {

    enum Expression { case neutral, happy, angry, sad, sleepy }

    // --- 仮想画面 (M5 の 320x240) ---
    private let vw: CGFloat = 320
    private let vh: CGFloat = 240
    private var cx: CGFloat { vw / 2 } // 160

    // --- 顔パラメータ（本家既定値）---
    private let eyeR: CGFloat = 8
    private let eyeSpread: CGFloat = 70
    private let eyeY: CGFloat = 94
    private let mouthY: CGFloat = 148
    private let minW: CGFloat = 50, maxW: CGFloat = 90
    private let minH: CGFloat = 4,  maxH: CGFloat = 60

    // --- アニメーション状態 ---
    private var t: Double = 0
    private var nextBlinkAt: Double = 0
    private var blinkUntil: Double = 0
    private var nextGazeAt: Double = 0
    private var gx: CGFloat = 0, gy: CGFloat = 0
    private var tgx: CGFloat = 0, tgy: CGFloat = 0

    // --- システム状態 ---
    private var expr: Expression = .neutral
    private var cpuLoad: Double = 0
    private var isCharging: Bool = false
    private var battery: Double = 1.0
    private var prevCPU: (busy: Double, total: Double)?
    private var sampleAccum: Double = 0

    // --- プレビュー用: キーで表情を固定（nil=自動） ---
    var testKeysEnabled = false
    var forcedExpr: Expression?

    override init?(frame: NSRect, isPreview: Bool) {
        super.init(frame: frame, isPreview: isPreview)
        animationTimeInterval = 1.0 / 60.0
        scheduleBlink(); scheduleGaze()
        sampleState(); updateExpression()
    }
    required init?(coder: NSCoder) {
        super.init(coder: coder)
        animationTimeInterval = 1.0 / 60.0
        scheduleBlink(); scheduleGaze()
        sampleState(); updateExpression()
    }

    private func scheduleBlink() { nextBlinkAt = t + 2.0 + Double.random(in: 0...4) }
    private func scheduleGaze()  { nextGazeAt  = t + 2.5 + Double.random(in: 0...4) }

    // MARK: - 状態取得（許可不要）

    private func sampleState() { sampleCPU(); samplePower() }

    private func sampleCPU() {
        var numCPUs: natural_t = 0
        var info: processor_info_array_t?
        var infoCount: mach_msg_type_number_t = 0
        let kr = host_processor_info(mach_host_self(), PROCESSOR_CPU_LOAD_INFO,
                                     &numCPUs, &info, &infoCount)
        guard kr == KERN_SUCCESS, let cpuInfo = info else { return }
        var busy = 0.0, total = 0.0
        let states = Int(CPU_STATE_MAX)
        for i in 0..<Int(numCPUs) {
            let u = Double(cpuInfo[i*states + Int(CPU_STATE_USER)])
            let s = Double(cpuInfo[i*states + Int(CPU_STATE_SYSTEM)])
            let n = Double(cpuInfo[i*states + Int(CPU_STATE_NICE)])
            let idle = Double(cpuInfo[i*states + Int(CPU_STATE_IDLE)])
            busy += u + s + n
            total += u + s + n + idle
        }
        vm_deallocate(mach_task_self_, vm_address_t(bitPattern: cpuInfo),
                      vm_size_t(infoCount) * vm_size_t(MemoryLayout<integer_t>.stride))
        if let p = prevCPU {
            let db = busy - p.busy, dt = total - p.total
            if dt > 0 { cpuLoad = max(0, min(1, db / dt)) }
        }
        prevCPU = (busy, total)
    }

    private func samplePower() {
        guard let snap = IOPSCopyPowerSourcesInfo()?.takeRetainedValue(),
              let list = IOPSCopyPowerSourcesList(snap)?.takeRetainedValue() as? [CFTypeRef]
        else { return }
        for src in list {
            guard let d = IOPSGetPowerSourceDescription(snap, src)?.takeUnretainedValue() as? [String: Any]
            else { continue }
            if let state = d[kIOPSPowerSourceStateKey] as? String {
                isCharging = (state == kIOPSACPowerValue)
            }
            if let cur = d[kIOPSCurrentCapacityKey] as? Int,
               let mx = d[kIOPSMaxCapacityKey] as? Int, mx > 0 {
                battery = Double(cur) / Double(mx)
            }
        }
    }

    private let sleepyAfterSec: Double = 300  // この秒数より長く表示され続けたら眠い（実運用=5分）

    private func updateExpression() {
        if let f = forcedExpr { expr = f; return }
        if cpuLoad > 0.7 { expr = .angry }            // CPU 使いすぎ
        else if isCharging { expr = .happy }          // 充電中
        else if battery < 0.2 { expr = .sad }         // 電池少ない
        else if t > sleepyAfterSec { expr = .sleepy } // 長時間表示
        else { expr = .neutral }
    }

    // MARK: - アニメーション

    override func animateOneFrame() {
        t += animationTimeInterval

        // 1秒ごとに状態をサンプリング
        sampleAccum += animationTimeInterval
        if sampleAccum >= 1.0 { sampleAccum = 0; sampleState(); updateExpression() }

        if t >= nextBlinkAt && t >= blinkUntil { blinkUntil = t + 0.12; scheduleBlink() }
        if t >= nextGazeAt { tgx = CGFloat.random(in: -1...1); tgy = CGFloat.random(in: -1...1); scheduleGaze() }
        let dt = CGFloat(animationTimeInterval)
        gx += (tgx - gx) * min(1, dt * 4)
        gy += (tgy - gy) * min(1, dt * 4)

        setNeedsDisplay(bounds)
    }

    // MARK: - 描画

    override func draw(_ rect: NSRect) {
        let W = bounds.width, H = bounds.height
        let scale = min(W / vw, H / vh)
        let offX = (W - vw * scale) / 2
        let offY = (H - vh * scale) / 2
        // 仮想座標(左上原点, y 下向き) → ビュー座標(左下原点)。
        // 本家 M5GFX と同じ左上原点の算術をそのまま渡せるようヘルパを用意。
        func px(_ x: CGFloat) -> CGFloat { offX + x * scale }
        func py(_ y: CGFloat) -> CGFloat { H - (offY + y * scale) }
        func vCircle(_ cxv: CGFloat, _ cyv: CGFloat, _ rv: CGFloat, _ color: NSColor) {
            color.setFill()
            NSBezierPath(ovalIn: NSRect(x: px(cxv) - rv * scale, y: py(cyv) - rv * scale,
                                        width: rv * 2 * scale, height: rv * 2 * scale)).fill()
        }
        func vRect(_ x0: CGFloat, _ y0: CGFloat, _ w: CGFloat, _ h: CGFloat, _ color: NSColor) {
            color.setFill()
            NSBezierPath(rect: NSRect(x: px(x0), y: py(y0 + h), width: w * scale, height: h * scale)).fill()
        }
        func vTriangle(_ p0: (CGFloat, CGFloat), _ p1: (CGFloat, CGFloat), _ p2: (CGFloat, CGFloat), _ color: NSColor) {
            color.setFill()
            let path = NSBezierPath()
            path.move(to: NSPoint(x: px(p0.0), y: py(p0.1)))
            path.line(to: NSPoint(x: px(p1.0), y: py(p1.1)))
            path.line(to: NSPoint(x: px(p2.0), y: py(p2.1)))
            path.close()
            path.fill()
        }

        NSColor.black.setFill()
        NSBezierPath(rect: bounds).fill()

        let open = t >= blinkUntil
        let goX = gx * 3, goY = gy * 3
        let white = NSColor.white, black = NSColor.black

        // 本家 Eye.cpp の draw() をそのまま移植（数値は本家と同一）
        func drawEye(_ ex: CGFloat, isLeft: Bool) {
            let exc = ex + goX, eyc = eyeY + goY, r = eyeR
            if !open {
                vRect(exc - r, eyc - 2, r * 2, 4, white)        // 閉じ = 横線
                return
            }
            vCircle(exc, eyc, r, white)                         // ベースの塗り円
            if expr == .angry || expr == .sad {                 // 目の上を斜め三角でカット
                let x0 = exc - r, y0 = eyc - r, x1 = exc + r
                let cond = (!isLeft) != !(expr == .sad)
                let x2 = cond ? x0 : x1
                vTriangle((x0, y0), (x1, y0), (x2, eyc), black)
            }
            if expr == .happy || expr == .sleepy {              // 下を矩形でマスク
                var y0 = eyc - r
                let x0 = exc - r, w = r * 2 + 4, h = r + 2
                if expr == .happy {                             // happy は中心も小円でくり抜き → ◠
                    y0 += r
                    vCircle(exc, eyc, r / 1.5, black)
                }
                vRect(x0, y0, w, h, black)
            }
        }
        drawEye(cx - eyeSpread, isLeft: false)  // 画面左（本家「右目」相当）
        drawEye(cx + eyeSpread, isLeft: true)   // 画面右（本家「左目」相当）

        // 口（本家どおり表情では変えない。待機=閉じ、呼吸で±2px）
        let breath = CGFloat(sin(t * 1.6))
        vRect(cx - maxW / 2, mouthY - minH / 2 + breath * 2, maxW, minH, white)
    }

    // MARK: - プレビューのキー操作（testKeysEnabled の時だけ有効）

    override var acceptsFirstResponder: Bool { testKeysEnabled }

    override func keyDown(with event: NSEvent) {
        guard testKeysEnabled else { super.keyDown(with: event); return }
        switch event.charactersIgnoringModifiers {
        case "n": forcedExpr = .neutral
        case "h": forcedExpr = .happy
        case "a": forcedExpr = .angry
        case "d": forcedExpr = .sad
        case "s": forcedExpr = .sleepy
        case "0", " ": forcedExpr = nil   // 自動（実際の状態に戻る）
        default: super.keyDown(with: event); return
        }
        let label = forcedExpr.map { "\($0)" } ?? "auto"
        Swift.print("expr -> \(label)")
        updateExpression()
        setNeedsDisplay(bounds)
    }

    override var hasConfigureSheet: Bool { false }
    override var configureSheet: NSWindow? { nil }
}
