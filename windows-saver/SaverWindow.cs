using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Stackchan {
    internal sealed class SaverWindow : Form {
        internal readonly Animation Animation = new Animation(Environment.TickCount);
        internal readonly RunMode Mode;
        internal event Action ExitRequested;
        internal bool AnimationRunning { get { return timer.Enabled; } }
        internal int FrameCount { get; private set; }
        internal int ResumeCount { get; private set; }
        private readonly IntPtr parent;
        private readonly Timer timer = new Timer();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly WindowsStateReader reader = new WindowsStateReader();
        private MouseDismissal mouse = new MouseDismissal();
        private SystemState state;
        private Expression? forced;
        private double lastTick, lastSample;
        private bool registered;

        internal SaverWindow(RunMode mode, IntPtr parentHandle) {
            Mode = mode; parent = parentHandle;
            Text = "Stackchan Preview  [N]通常 [H]嬉しい [A]怒り [D]悲しい [S]眠い [Space]自動 [Esc]終了";
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(640, 480);
            BackColor = Color.Black;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
            KeyPreview = true;
            if (mode == RunMode.Preview) {
                MinimumSize = new Size(320, 260);
                StartPosition = FormStartPosition.CenterScreen;
            } else {
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.Manual;
                // Keep WinForms' managed window state consistent with the native WS_CHILD style.
                if (mode == RunMode.Embedded) TopLevel = false;
                if (mode == RunMode.Saver) {
                    TopMost = true;
                    mouse.Move(Cursor.Position.X, Cursor.Position.Y);
                }
            }
            state = reader.Read();
            timer.Interval = 33; // 30 fps keeps a simple idle face inexpensive.
            timer.Tick += TickFrame;
        }

        protected override CreateParams CreateParams {
            get {
                CreateParams cp = base.CreateParams;
                if (Mode == RunMode.Embedded) {
                    cp.Style = (cp.Style & ~unchecked((int)0x80000000)) | 0x40000000;
                    cp.Parent = parent;
                }
                return cp;
            }
        }
        protected override bool ShowWithoutActivation { get { return Mode == RunMode.Embedded; } }
        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);
            if (Mode != RunMode.Embedded) registered = Native.WTSRegisterSessionNotification(Handle, 0);
            else ResizeToParent();
            lastTick = clock.Elapsed.TotalSeconds;
            timer.Start();
        }
        protected override void OnHandleDestroyed(EventArgs e) {
            timer.Stop();
            if (registered) { Native.WTSUnRegisterSessionNotification(Handle); registered = false; }
            base.OnHandleDestroyed(e);
            // Destroying a foreign parent destroys its native child HWND without FormClosed.
            // The animation timer is already stopped, so notify the session here as well.
            if (Mode == RunMode.Embedded && !RecreatingHandle && !Disposing && !IsDisposed) RequestExit();
        }
        private void ResizeToParent() {
            Native.Rect rect;
            if (!Native.IsWindow(parent) || !Native.GetClientRect(parent, out rect)) { RequestExit(); return; }
            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, rect.Right - rect.Left), Math.Max(1, rect.Bottom - rect.Top));
            if (Bounds != bounds) Bounds = bounds;
        }
        private void TickFrame(object sender, EventArgs e) {
            if (Mode == RunMode.Embedded) {
                ResizeToParent();
                if (IsDisposed || Disposing) return;
            }
            if (Mode == RunMode.Saver && mouse.Move(Cursor.Position.X, Cursor.Position.Y)) { RequestExit(); return; }
            double now = clock.Elapsed.TotalSeconds;
            Animation.Advance(now - lastTick); lastTick = now;
            if (now - lastSample >= 1) { state = reader.Read(); lastSample = now; }
            FrameCount++;
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            Expression expression = forced ?? ExpressionPolicy.Select(state, Animation.Elapsed);
            FaceRenderer.Draw(e.Graphics, ClientSize, expression, Animation.EyesOpen, Animation.Elapsed, Animation.GazeX, Animation.GazeY);
            if (Mode == RunMode.Preview) {
                string cpu = state.Cpu.HasValue ? (state.Cpu.Value * 100).ToString("F0") + "%" : "--";
                string battery = state.Battery.HasValue ? (state.Battery.Value * 100).ToString("F0") + "%" : "--";
                string status = (forced.HasValue ? "固定: " : "自動: ") + expression + "    CPU " + cpu + "    AC " + (state.OnAc ? "ON" : "OFF") + "    電池 " + battery;
                TextRenderer.DrawText(e.Graphics, status, Font, new Rectangle(8, Math.Max(0, ClientSize.Height - 27), Math.Max(0, ClientSize.Width - 16), 24), Color.Gray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
        protected override void OnKeyDown(KeyEventArgs e) {
            if (Mode == RunMode.Preview) {
                switch (e.KeyCode) {
                    case Keys.N: forced = Expression.Neutral; break;
                    case Keys.H: forced = Expression.Happy; break;
                    case Keys.A: forced = Expression.Angry; break;
                    case Keys.D: forced = Expression.Sad; break;
                    case Keys.S: forced = Expression.Sleepy; break;
                    case Keys.Space: case Keys.D0: forced = null; break;
                    case Keys.Escape: RequestExit(); return;
                    default: base.OnKeyDown(e); return;
                }
                e.Handled = true; Invalidate();
            }
            base.OnKeyDown(e);
        }
        private void RequestExit() {
            if (ExitRequested != null) ExitRequested();
            else Close();
        }
        private void ResumeAnimation() {
            Animation.Resume();
            reader.Reset(); state = reader.Read();
            lastSample = lastTick = clock.Elapsed.TotalSeconds;
            mouse = new MouseDismissal(); mouse.Move(Cursor.Position.X, Cursor.Position.Y);
            ResumeCount++;
            timer.Start(); Invalidate();
        }
        protected override void WndProc(ref Message m) {
            if (m.Msg == Native.WM_POWERBROADCAST) {
                int kind = m.WParam.ToInt32();
                if (kind == 4) { Animation.Suspend(); timer.Stop(); }
                else if (kind == 18 || kind == 7) ResumeAnimation();
                else if (kind == 10) state = reader.Read();
                m.Result = new IntPtr(1); return;
            }
            if (m.Msg == Native.WM_WTSSESSION_CHANGE) {
                if (Mode == RunMode.Saver) { RequestExit(); return; }
                if (Mode == RunMode.Preview) ResumeAnimation();
            }
            if (m.Msg == Native.WM_DISPLAYCHANGE && Mode == RunMode.Saver) { RequestExit(); return; }
            if (Mode == RunMode.Saver) {
                // Only hide the cursor over our own window. No global ShowCursor counter or input hooks.
                if (m.Msg == 0x20) { Native.SetCursor(IntPtr.Zero); m.Result = new IntPtr(1); return; }
                if (m.Msg == 0x100 || m.Msg == 0x104 || m.Msg == 0x201 || m.Msg == 0x204 || m.Msg == 0x207 || m.Msg == 0x20a || m.Msg == 0x20b) {
                    RequestExit(); return;
                }
                if (m.Msg == 0x112 && (m.WParam.ToInt64() & 0xfff0) == 0xf140) { m.Result = IntPtr.Zero; return; }
            }
            base.WndProc(ref m);
        }
        protected override void Dispose(bool disposing) {
            if (disposing) { timer.Stop(); timer.Dispose(); }
            base.Dispose(disposing);
        }
    }

    internal sealed class SaverSession : ApplicationContext {
        private readonly System.Collections.Generic.List<SaverWindow> windows = new System.Collections.Generic.List<SaverWindow>();
        private bool exiting;
        internal SaverWindow[] Windows { get { return windows.ToArray(); } }
        internal SaverSession(Options options) : this(options, options.Mode == RunMode.Saver ? DisplayBounds() : new Rectangle[0], true) { }
        private static Rectangle[] DisplayBounds() {
            Screen[] screens = Screen.AllScreens;
            Rectangle[] bounds = new Rectangle[screens.Length];
            for (int i = 0; i < screens.Length; i++) bounds[i] = screens[i].Bounds;
            return bounds;
        }
        // Tests inject monitor rectangles and create hidden HWNDs without covering the desktop.
        internal SaverSession(Options options, Rectangle[] displayBounds, bool show) {
            if (options.Mode == RunMode.Saver) {
                foreach (Rectangle bounds in displayBounds) {
                    SaverWindow window = AddWindow(options);
                    window.Bounds = bounds;
                }
            } else AddWindow(options);
            if (show) foreach (SaverWindow window in windows.ToArray()) window.Show();
        }
        private SaverWindow AddWindow(Options options) {
            SaverWindow window = new SaverWindow(options.Mode, options.Parent);
            window.ExitRequested += CloseAll;
            window.FormClosed += delegate { CloseAll(); };
            windows.Add(window);
            return window;
        }
        private void CloseAll() {
            if (exiting) return;
            exiting = true;
            foreach (SaverWindow window in windows.ToArray()) if (!window.IsDisposed) window.Close();
            ExitThread();
        }
        protected override void Dispose(bool disposing) {
            if (disposing) foreach (SaverWindow window in windows) window.Dispose();
            base.Dispose(disposing);
        }
    }
}
