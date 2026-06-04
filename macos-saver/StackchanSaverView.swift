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

    enum Expression { case neutral, happy, angry, sleepy }

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

    private func updateExpression() {
        if cpuLoad > 0.7 { expr = .angry }
        else if isCharging { expr = .happy }
        else if battery < 0.2 { expr = .sleepy }
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
        func px(_ x: CGFloat) -> CGFloat { offX + x * scale }
        func py(_ y: CGFloat) -> CGFloat { H - (offY + y * scale) } // y 反転
        func pr(_ r: CGFloat) -> CGFloat { r * scale }

        NSColor.black.setFill()
        NSBezierPath(rect: bounds).fill()

        let open = t >= blinkUntil
        let goX = gx * 3, goY = gy * 3

        // 両目
        for ex in [cx - eyeSpread, cx + eyeSpread] {
            drawEye(ex: ex, goX: goX, goY: goY, open: open, px: px, py: py, pr: pr)
        }

        // 怒り顔: 眉
        if expr == .angry {
            NSColor.white.setStroke()
            for (i, ex) in [cx - eyeSpread, cx + eyeSpread].enumerated() {
                let inner = (i == 0) ? ex + 14 : ex - 14   // 中央寄り = 低い
                let outer = (i == 0) ? ex - 14 : ex + 14   // 外側 = 高い
                let path = NSBezierPath()
                path.lineWidth = pr(4)
                path.lineCapStyle = .round
                path.move(to: NSPoint(x: px(outer + goX), y: py(eyeY - 24 + goY)))
                path.line(to: NSPoint(x: px(inner + goX), y: py(eyeY - 13 + goY)))
                path.stroke()
            }
        }

        // 口（happy は開いて笑う / 呼吸で±2px）
        let mouthOpen: CGFloat = (expr == .happy) ? 0.6 : 0.0
        let breath = CGFloat(sin(t * 1.6))
        let w = minW + (maxW - minW) * (1 - mouthOpen)
        let h = minH + (maxH - minH) * mouthOpen
        NSColor.white.setFill()
        let mcx = px(cx), mcy = py(mouthY + breath * 2)
        NSBezierPath(rect: NSRect(x: mcx - pr(w) / 2, y: mcy - pr(h) / 2, width: pr(w), height: pr(h))).fill()
    }

    private func drawEye(ex: CGFloat, goX: CGFloat, goY: CGFloat, open: Bool,
                         px: (CGFloat) -> CGFloat, py: (CGFloat) -> CGFloat, pr: (CGFloat) -> CGFloat) {
        let ecx = px(ex + goX)
        let ecy = py(eyeY + goY)
        let r = pr(eyeR)

        NSColor.white.setFill()
        if !open {
            // まばたき = 横線（表情によらず）
            let w = pr(eyeR * 2), h = pr(4)
            NSBezierPath(rect: NSRect(x: ecx - w / 2, y: ecy - h / 2, width: w, height: h)).fill()
            return
        }

        // ベースの塗り円
        NSBezierPath(ovalIn: NSRect(x: ecx - r, y: ecy - r, width: r * 2, height: r * 2)).fill()

        // 表情ごとに背景色で削る
        NSColor.black.setFill()
        switch expr {
        case .happy:
            // 下半分を消して ◠（笑い目）
            NSBezierPath(rect: NSRect(x: ecx - r, y: ecy - r, width: r * 2, height: r)).fill()
        case .sleepy:
            // 上 7 割を消して半目（下のスリットだけ残す）
            NSBezierPath(rect: NSRect(x: ecx - r, y: ecy - r * 0.4, width: r * 2, height: r * 1.4)).fill()
        case .angry, .neutral:
            break
        }
    }

    override var hasConfigureSheet: Bool { false }
    override var configureSheet: NSWindow? { nil }
}
