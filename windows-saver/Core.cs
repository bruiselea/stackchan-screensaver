using System;
using System.Globalization;

namespace Stackchan {
    public enum Expression { Neutral, Happy, Angry, Sad, Sleepy }
    public enum RunMode { Configure, Preview, Saver, Embedded }

    public sealed class Options {
        public RunMode Mode;
        public IntPtr Parent;
        public static Options Parse(string[] args, bool isScr) {
            if (args.Length == 0) return new Options { Mode = isScr ? RunMode.Configure : RunMode.Preview };
            if (args.Length == 1 && args[0] == "--preview") return new Options { Mode = RunMode.Preview };
            string[] parts = args[0].ToLowerInvariant().Split(new char[] { ':' }, 2);
            string flag = parts[0];
            if (flag == "/s" || flag == "-s") {
                if (args.Length != 1 || parts.Length != 1) throw new ArgumentException("Invalid /s arguments.");
                return new Options { Mode = RunMode.Saver };
            }
            bool preview = flag == "/p" || flag == "-p";
            bool config = flag == "/c" || flag == "-c";
            if (!preview && !config) throw new ArgumentException("Use --preview, /s, /p HWND or /c.");
            if (args.Length > 2 || (parts.Length == 2 && args.Length != 1)) throw new ArgumentException("Too many arguments.");
            string handle = parts.Length == 2 ? parts[1] : args.Length == 2 ? args[1] : null;
            long value = 0;
            if (handle != null && (!long.TryParse(handle, NumberStyles.None, CultureInfo.InvariantCulture, out value)
                || value < 0 || (IntPtr.Size == 4 && value > int.MaxValue))) throw new ArgumentException("Invalid window handle.");
            if (preview && value == 0) throw new ArgumentException("Preview requires a parent window.");
            return new Options { Mode = preview ? RunMode.Embedded : RunMode.Configure, Parent = new IntPtr(value) };
        }
    }

    public sealed class SystemState {
        public double? Cpu;
        public bool OnAc;
        public double? Battery;
    }

    public static class ExpressionPolicy {
        public static Expression Select(SystemState state, double elapsed) {
            if (state.Cpu.HasValue && state.Cpu.Value > 0.7) return Expression.Angry;
            if (state.OnAc) return Expression.Happy;
            if (state.Battery.HasValue && state.Battery.Value < 0.2) return Expression.Sad;
            return elapsed > 300 ? Expression.Sleepy : Expression.Neutral;
        }
    }

    public sealed class Animation {
        private readonly Random random;
        private double nextBlink, blinkUntil, nextGaze, targetX, targetY;
        public double Elapsed { get; private set; }
        public double GazeX { get; private set; }
        public double GazeY { get; private set; }
        public bool Suspended { get; private set; }
        public bool EyesOpen { get { return Elapsed >= blinkUntil; } }
        public Animation(int seed) { random = new Random(seed); Reschedule(); }
        private void Reschedule() {
            nextBlink = Elapsed + 2 + random.NextDouble() * 4;
            nextGaze = Elapsed + 2.5 + random.NextDouble() * 4;
            blinkUntil = Elapsed;
        }
        public void Advance(double seconds) {
            if (Suspended || seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            // A delayed frame must not create a large animation jump (e.g. after sleep).
            double step = Math.Min(seconds, 0.1);
            Elapsed += step;
            if (Elapsed >= nextBlink) {
                blinkUntil = Elapsed + 0.12;
                nextBlink = Elapsed + 2 + random.NextDouble() * 4;
            }
            if (Elapsed >= nextGaze) {
                targetX = random.NextDouble() * 2 - 1; targetY = random.NextDouble() * 2 - 1;
                nextGaze = Elapsed + 2.5 + random.NextDouble() * 4;
            }
            GazeX += (targetX - GazeX) * Math.Min(1, step * 4);
            GazeY += (targetY - GazeY) * Math.Min(1, step * 4);
        }
        public void Suspend() { Suspended = true; }
        public void Resume() { Suspended = false; Reschedule(); }
    }

    public sealed class MouseDismissal {
        private bool initialized;
        private int x, y;
        public bool Move(int newX, int newY) {
            if (!initialized) { initialized = true; x = newX; y = newY; return false; }
            // Compare against the initial point, so slow movement also dismisses.
            return Math.Abs((long)newX - x) > 5 || Math.Abs((long)newY - y) > 5;
        }
    }
}
