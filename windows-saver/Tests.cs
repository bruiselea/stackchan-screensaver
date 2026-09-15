using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Stackchan {
    internal static class Tests {
        private static readonly List<string> report = new List<string>();
        private static int failures;
        private static string output, saverPath;
        private delegate bool EnumWindow(IntPtr handle, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumWindow callback, IntPtr parameter);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
        [DllImport("user32.dll")] private static extern uint GetGuiResources(IntPtr process, uint flags);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

        [STAThread]
        private static int Main(string[] args) {
            output = args[0]; saverPath = args[1];
            Directory.CreateDirectory(output);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Test("Windows command line: /s, /c, /p, colon, case, safe default", Arguments);
            Test("Expression priority and exact 70%, 20%, 300-second boundaries", Expressions);
            Test("AC, absent battery, unknown status and charge percentage", Power);
            Test("CPU deltas, idle accounting, counter resets and resume baseline", Cpu);
            Test("Animation: blinks, gaze, breathing time, suspend, resume and long frames", AnimationState);
            Test("Mouse dismissal: jitter ignored; accumulated slow movement exits", Mouse);
            Test("Rendering: all faces, eye masks, blink and aspect ratio", Render);
            Test("Real Windows CPU and power APIs", ReadWindows);
            Test("Hidden window: actual suspend/resume/power/session messages and timer", Lifecycle);
            Test("Hidden saver: keyboard/mouse/display/session exit messages", ExitMessages);
            Test("Simulated dual monitors: negative coordinates and all-window exit", MultipleMonitors);
            Test("Real .scr process x5: embedded preview, resize, input, parent destruction", delegate { for (int i = 0; i < 5; i++) EmbeddedProcess(); });
            Test("Real .scr process: invalid arguments fail without UI", InvalidProcess);
            Test("Resource stability over 2400 frames and 30 window lifecycles", Resources);
            Test("Test windows do not own the foreground", delegate {
                uint process; GetWindowThreadProcessId(GetForegroundWindow(), out process);
                Assert(process != Process.GetCurrentProcess().Id, "A test window was activated.");
            });
            report.Add("\nNo OS sleep, lock, display-power change, screensaver registration, registry write, broadcast or global input injection was performed.");
            report.Add("Hardware sleep/resume, actual fullscreen/RDP reconnect, multiple physical displays and Windows idle activation remain unverified.");
            report.Add("OS: " + Environment.OSVersion + "; CLR: " + Environment.Version + "; 64-bit process: " + Environment.Is64BitProcess);
            report.Add("Result: " + (failures == 0 ? "PASS" : "FAIL") + " (" + failures + " failed groups)");
            File.WriteAllLines(Path.Combine(output, "report.txt"), report.ToArray());
            foreach (string line in report) Console.WriteLine(line);
            return failures == 0 ? 0 : 1;
        }
        private static void Test(string name, Action action) {
            try { action(); report.Add("PASS " + name); }
            catch (Exception ex) { failures++; report.Add("FAIL " + name + ": " + ex); }
        }
        private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Arguments() {
            Assert(Options.Parse(new string[0], false).Mode == RunMode.Preview, "EXE must default to a window.");
            Assert(Options.Parse(new string[0], true).Mode == RunMode.Configure, "SCR default must configure.");
            Assert(Options.Parse(new string[] { "/S" }, true).Mode == RunMode.Saver, "/S");
            Assert(Options.Parse(new string[] { "--preview" }, true).Mode == RunMode.Preview, "--preview");
            Assert(Options.Parse(new string[] { "/p", "123456" }, true).Parent.ToInt64() == 123456, "/p handle");
            Assert(Options.Parse(new string[] { "-P:123456" }, true).Mode == RunMode.Embedded, "-P:handle");
            Assert(Options.Parse(new string[] { "/c:123" }, true).Mode == RunMode.Configure, "/c:handle");
            Assert(Options.Parse(new string[] { "/c" }, true).Parent == IntPtr.Zero, "/c");
            string[][] invalid = { new string[] { "/p" }, new string[] { "/p:0" }, new string[] { "/p:-1" }, new string[] { "/p:foo" },
                new string[] { "/unknown" }, new string[] { "/s", "123" }, new string[] { "/s:1" }, new string[] { "/p:2", "3" }, new string[] { "/c:" } };
            foreach (string[] values in invalid) {
                bool rejected = false;
                try { Options.Parse(values, true); } catch (ArgumentException) { rejected = true; }
                Assert(rejected, "Accepted " + string.Join(" ", values));
            }
        }
        private static void Expressions() {
            SystemState state = new SystemState();
            Assert(ExpressionPolicy.Select(state, 0) == Expression.Neutral, "Unknown state neutral");
            Assert(ExpressionPolicy.Select(state, 300) == Expression.Neutral, "300s boundary");
            Assert(ExpressionPolicy.Select(state, 301) == Expression.Sleepy, "Sleepy");
            state.Battery = 0.19;
            Assert(ExpressionPolicy.Select(state, 301) == Expression.Sad, "Battery priority");
            state.Battery = 0.2;
            Assert(ExpressionPolicy.Select(state, 0) == Expression.Neutral, "20% boundary");
            state.OnAc = true; state.Battery = 0.01;
            Assert(ExpressionPolicy.Select(state, 301) == Expression.Happy, "AC priority");
            state.Cpu = 0.7;
            Assert(ExpressionPolicy.Select(state, 301) == Expression.Happy, "70% boundary");
            state.Cpu = 0.701;
            Assert(ExpressionPolicy.Select(state, 301) == Expression.Angry, "CPU priority");
        }
        private static void Power() {
            Assert(WindowsStateReader.DecodePower(1, 128, 255).OnAc, "Desktop AC");
            Assert(!WindowsStateReader.DecodePower(1, 128, 0).Battery.HasValue, "Absent battery");
            Assert(!WindowsStateReader.DecodePower(255, 255, 255).OnAc, "Unknown AC");
            Assert(!WindowsStateReader.DecodePower(0, 255, 10).Battery.HasValue, "Unknown battery flags");
            Assert(WindowsStateReader.DecodePower(0, 0, 19).Battery == 0.19, "19% battery");
            Assert(!WindowsStateReader.DecodePower(0, 0, 255).Battery.HasValue, "Unknown percentage");
            Assert(WindowsStateReader.DecodePower(1, 8, 50).OnAc, "Charging");
        }
        private static void Cpu() {
            CpuCounter counter = new CpuCounter();
            Assert(!counter.Sample(100, 200, 100).HasValue, "First sample must be unknown");
            double? load = counter.Sample(125, 250, 150);
            Assert(load.HasValue && Math.Abs(load.Value - 0.75) < 0.000001, "Kernel time includes idle");
            Assert(!counter.Sample(125, 250, 150).HasValue, "Zero delta");
            Assert(!counter.Sample(1, 2, 1).HasValue, "Regressing counters");
            counter.Reset();
            Assert(!counter.Sample(5000, 6000, 7000).HasValue, "Resume baseline");
        }
        private static void AnimationState() {
            Animation animation = new Animation(42);
            bool blink = false, gaze = false;
            for (int i = 0; i < 2000; i++) {
                animation.Advance(1.0 / 30);
                blink |= !animation.EyesOpen;
                gaze |= Math.Abs(animation.GazeX) > 0.1;
                Assert(Math.Abs(animation.GazeX) <= 1 && Math.Abs(animation.GazeY) <= 1, "Gaze bounds");
            }
            Assert(blink && gaze, "Animation must change");
            animation.Suspend(); double before = animation.Elapsed;
            animation.Advance(600);
            Assert(animation.Elapsed == before, "Suspended time advances");
            animation.Resume();
            Assert(animation.EyesOpen && !animation.Suspended, "Resume resets blink");
            animation.Advance(600);
            Assert(animation.Elapsed - before <= 0.10001, "Large frame must be clamped");
            animation.Advance(double.NaN); animation.Advance(double.PositiveInfinity);
            Assert(!double.IsNaN(animation.Elapsed), "Finite state");
            for (int i = 0; i < 10000; i++) animation.Advance(1.0 / 30);
            Assert(ExpressionPolicy.Select(new SystemState(), animation.Elapsed) == Expression.Sleepy, "Five minutes simulated without waiting");
        }
        private static void Mouse() {
            MouseDismissal input = new MouseDismissal();
            Assert(!input.Move(-100, 300), "Initial mouse event");
            for (int x = -99; x <= -95; x++) Assert(!input.Move(x, 300), "Small jitter");
            Assert(input.Move(-94, 300), "Slow movement must accumulate");
        }
        private static Bitmap Face(Expression expression, bool open, int width, int height) {
            Bitmap bitmap = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bitmap)) FaceRenderer.Draw(g, bitmap.Size, expression, open, 0, 0, 0);
            return bitmap;
        }
        private static bool White(Bitmap image, int x, int y) { return image.GetPixel(x, y).R > 200; }
        private static void Render() {
            using (Bitmap sheet = new Bitmap(960, 520))
            using (Graphics g = Graphics.FromImage(sheet)) {
                g.Clear(Color.FromArgb(24, 24, 24));
                Expression[] expressions = (Expression[])Enum.GetValues(typeof(Expression));
                for (int i = 0; i < 6; i++) {
                    Expression expression = expressions[i % 5]; bool open = i != 5;
                    using (Bitmap face = Face(expression, open, 320, 240)) {
                        int x = i % 3 * 320, y = i / 3 * 260;
                        g.DrawImageUnscaled(face, x, y);
                        g.DrawString(open ? expression.ToString() : "Blink", SystemFonts.DefaultFont, Brushes.Silver, x + 12, y + 240);
                        face.Save(Path.Combine(output, open ? expression + ".png" : "Blink.png"), ImageFormat.Png);
                        Assert(White(face, 160, 148), "Mouth missing");
                        Assert(!White(face, 0, 0), "Background");
                    }
                }
                sheet.Save(Path.Combine(output, "faces.png"), ImageFormat.Png);
            }
            using (Bitmap neutral = Face(Expression.Neutral, true, 320, 240)) Assert(White(neutral, 90, 94) && White(neutral, 230, 94), "Neutral eyes");
            using (Bitmap happy = Face(Expression.Happy, true, 320, 240)) Assert(!White(happy, 90, 94) && White(happy, 90, 87), "Happy arc");
            using (Bitmap sleepy = Face(Expression.Sleepy, true, 320, 240)) Assert(!White(sleepy, 90, 90) && White(sleepy, 90, 99), "Sleepy lower half");
            using (Bitmap angry = Face(Expression.Angry, true, 320, 240))
            using (Bitmap sad = Face(Expression.Sad, true, 320, 240)) Assert(White(angry, 85, 91) && !White(sad, 85, 91), "Angry/sad masks must oppose");
            using (Bitmap blink = Face(Expression.Neutral, false, 320, 240)) Assert(White(blink, 90, 94) && !White(blink, 90, 89), "Blink line");
            using (Bitmap wide = Face(Expression.Neutral, true, 1280, 720)) {
                Assert(White(wide, 430, 282) && White(wide, 850, 282), "Wide centered scale");
                wide.Save(Path.Combine(output, "widescreen.png"), ImageFormat.Png);
            }
            using (Bitmap portrait = Face(Expression.Neutral, true, 240, 640)) {
                Assert(White(portrait, 67, 300) && White(portrait, 172, 300), "Portrait centered scale");
            }
        }
        private static void Pump(int milliseconds) {
            Stopwatch watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(10); }
        }
        private static void ReadWindows() {
            WindowsStateReader reader = new WindowsStateReader(); reader.Read(); Pump(1100);
            SystemState state = reader.Read();
            Assert(state.Cpu.HasValue && state.Cpu >= 0 && state.Cpu <= 1, "Windows CPU sampling unavailable");
            Native.PowerStatus power;
            Assert(Native.GetSystemPowerStatus(out power), "Windows power API unavailable");
            report.Add("INFO Real state: CPU=" + state.Cpu.Value.ToString("P1") + ", AC=" + state.OnAc + ", battery=" + (state.Battery.HasValue ? state.Battery.Value.ToString("P0") : "unknown/absent"));
        }
        private static void Send(Form window, int message, int parameter) { Native.SendMessage(window.Handle, message, new IntPtr(parameter), IntPtr.Zero); }
        private static void Lifecycle() {
            using (SaverWindow window = new SaverWindow(RunMode.Preview, IntPtr.Zero)) {
                IntPtr handle = window.Handle; // Create a real HWND, but never show or activate it.
                Pump(160); Assert(window.FrameCount > 0, "Timer not ticking");
                Send(window, Native.WM_POWERBROADCAST, 4);
                int frames = window.FrameCount; double elapsed = window.Animation.Elapsed;
                Pump(150);
                Assert(window.Animation.Suspended && !window.AnimationRunning && window.FrameCount == frames, "Suspend did not pause");
                Send(window, Native.WM_POWERBROADCAST, 18);
                Pump(160);
                Assert(!window.Animation.Suspended && window.AnimationRunning && window.FrameCount > frames, "Resume did not animate");
                Assert(window.Animation.Elapsed - elapsed < 0.4, "Suspended wall time leaked into animation");
                Send(window, Native.WM_POWERBROADCAST, 7);
                Send(window, Native.WM_POWERBROADCAST, 10);
                Send(window, Native.WM_WTSSESSION_CHANGE, 3);
                Assert(window.ResumeCount == 3 && !window.IsDisposed, "Duplicate resume/remote connect");
                for (int i = 0; i < 20; i++) { Send(window, Native.WM_POWERBROADCAST, 4); Send(window, Native.WM_POWERBROADCAST, 18); }
                Assert(window.AnimationRunning, "Repeated resume failed");
                using (Bitmap bitmap = new Bitmap(window.Width, window.Height)) { window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, window.Size)); bitmap.Save(Path.Combine(output, "window-after-resume.png")); }
            }
        }
        private static void ExitMessages() {
            int[,] messages = { {0x100, 27}, {0x104, 18}, {0x201, 0}, {0x204, 0}, {0x207, 0}, {0x20a, 0}, {Native.WM_DISPLAYCHANGE, 32}, {Native.WM_WTSSESSION_CHANGE, 4}, {Native.WM_WTSSESSION_CHANGE, 7} };
            for (int i = 0; i < messages.GetLength(0); i++) {
                using (SaverWindow window = new SaverWindow(RunMode.Saver, IntPtr.Zero)) {
                    window.TopMost = false; bool requested = false;
                    window.ExitRequested += delegate { requested = true; };
                    Send(window, messages[i, 0], messages[i, 1]);
                    Assert(requested, "Exit not requested for message " + messages[i, 0]);
                }
            }
        }
        private static Process Launch(string arguments) {
            return Process.Start(new ProcessStartInfo(saverPath, arguments) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
        }
        private static void MultipleMonitors() {
            Rectangle[] bounds = { new Rectangle(-1280, 0, 1280, 720), new Rectangle(0, -180, 1920, 1080) };
            using (SaverSession session = new SaverSession(new Options { Mode = RunMode.Saver }, bounds, false)) {
                SaverWindow[] windows = session.Windows;
                Assert(windows.Length == 2, "One window per monitor");
                for (int i = 0; i < windows.Length; i++) {
                    Assert(windows[i].Bounds == bounds[i], "Monitor bounds " + i);
                    windows[i].TopMost = false;
                    IntPtr handle = windows[i].Handle;
                    Assert(!windows[i].Visible, "Test saver became visible");
                }
                Send(windows[1], 0x100, 27);
                Assert(windows[0].IsDisposed && windows[1].IsDisposed, "Input on secondary monitor must close both");
            }
            using (SaverSession session = new SaverSession(new Options { Mode = RunMode.Saver }, bounds, false)) {
                SaverWindow[] windows = session.Windows;
                foreach (SaverWindow window in windows) { window.TopMost = false; IntPtr handle = window.Handle; }
                Send(windows[0], Native.WM_DISPLAYCHANGE, 32);
                Assert(windows[0].IsDisposed && windows[1].IsDisposed, "Display change must close both");
            }
        }
        private static IntPtr FindChild(IntPtr host, int process) {
            IntPtr found = IntPtr.Zero;
            EnumChildWindows(host, delegate(IntPtr child, IntPtr ignored) {
                uint id; GetWindowThreadProcessId(child, out id);
                if (id == process) { found = child; return false; }
                return true;
            }, IntPtr.Zero);
            return found;
        }
        private static void EmbeddedProcess() {
            using (Form host = new Form()) {
                host.ClientSize = new Size(320, 240);
                IntPtr handle = host.Handle;
                using (Process process = Launch("/p " + handle.ToInt64())) {
                    try {
                        IntPtr child = IntPtr.Zero;
                        Stopwatch watch = Stopwatch.StartNew();
                        // Wait for asynchronous startup while continuing to service the host HWND.
                        while (child == IntPtr.Zero && watch.ElapsedMilliseconds < 15000 && !process.HasExited) { Pump(30); child = FindChild(handle, process.Id); }
                        Assert(child != IntPtr.Zero, "Preview child HWND not found; parent valid=" + Native.IsWindow(handle)
                            + ", child process=" + (process.HasExited ? "exited " + process.ExitCode : "running after 15 seconds"));
                        Assert(Native.GetParent(child) == handle, "Preview not parented");
                        Native.Rect rect; Native.GetClientRect(child, out rect);
                        Assert(rect.Right == 320 && rect.Bottom == 240, "Initial preview dimensions");
                        host.ClientSize = new Size(420, 260); Pump(300);
                        Native.GetClientRect(child, out rect);
                        Assert(rect.Right == 420 && rect.Bottom == 260, "Preview resize");
                        Native.SendMessage(child, 0x100, new IntPtr(27), IntPtr.Zero); Pump(80);
                        Assert(!process.HasExited, "Embedded preview must ignore exit keys");
                        Native.SendMessage(child, Native.WM_POWERBROADCAST, new IntPtr(4), IntPtr.Zero);
                        Native.SendMessage(child, Native.WM_POWERBROADCAST, new IntPtr(18), IntPtr.Zero); Pump(80);
                        Assert(!process.HasExited, "Embedded resume failed");
                        host.Dispose();
                        Assert(process.WaitForExit(4000), "Preview process survived parent destruction");
                        Assert(process.ExitCode == 0, "Preview process failed: " + process.ExitCode);
                    } finally { if (!process.HasExited) { process.Kill(); process.WaitForExit(); } }
                }
            }
        }
        private static void InvalidProcess() {
            foreach (string arguments in new string[] { "/p:0", "/p:123", "--invalid", "/p" }) {
                using (Process process = Launch(arguments)) {
                    try { Assert(process.WaitForExit(10000) && process.ExitCode == 2, "Invalid arguments did not exit: " + arguments); }
                    finally { if (!process.HasExited) { process.Kill(); process.WaitForExit(); } }
                }
            }
        }
        private static void Resources() {
            uint before = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
            using (Bitmap image = new Bitmap(640, 480))
            using (Graphics graphics = Graphics.FromImage(image)) {
                for (int i = 0; i < 2400; i++) FaceRenderer.Draw(graphics, image.Size, (Expression)(i % 5), i % 90 != 0, i / 30.0, 0.3, -0.2);
            }
            for (int i = 0; i < 30; i++) using (SaverWindow window = new SaverWindow(RunMode.Preview, IntPtr.Zero)) { IntPtr handle = window.Handle; }
            GC.Collect(); GC.WaitForPendingFinalizers();
            uint after = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
            Assert(after <= before + 10, "GDI handles grew: " + before + " -> " + after);
            report.Add("INFO GDI handles: " + before + " -> " + after);
        }
    }
}
