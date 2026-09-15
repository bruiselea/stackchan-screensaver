using System;
using System.Runtime.InteropServices;

namespace Stackchan {
    internal static class Native {
        internal const int WM_POWERBROADCAST = 0x218, WM_DISPLAYCHANGE = 0x7e, WM_WTSSESSION_CHANGE = 0x2b1;
        [StructLayout(LayoutKind.Sequential)] internal struct FileTime {
            public uint Low, High;
            public ulong Value { get { return ((ulong)High << 32) | Low; } }
        }
        [StructLayout(LayoutKind.Sequential)] internal struct PowerStatus {
            public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
            public uint BatteryLifeTime, BatteryFullLifeTime;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool GetSystemPowerStatus(out PowerStatus status);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr handle);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool GetClientRect(IntPtr handle, out Rect rect);
        [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr handle);
        [DllImport("user32.dll")] internal static extern IntPtr SetCursor(IntPtr cursor);
        [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("wtsapi32.dll", SetLastError = true)] internal static extern bool WTSRegisterSessionNotification(IntPtr handle, uint flags);
        [DllImport("wtsapi32.dll")] internal static extern bool WTSUnRegisterSessionNotification(IntPtr handle);
    }

    public sealed class CpuCounter {
        private ulong idle, kernel, user;
        private bool primed;
        public void Reset() { primed = false; }
        public double? Sample(ulong nextIdle, ulong nextKernel, ulong nextUser) {
            double? result = null;
            if (primed && nextIdle >= idle && nextKernel >= kernel && nextUser >= user) {
                double total = (double)(nextKernel - kernel) + (double)(nextUser - user);
                double idleDelta = nextIdle - idle;
                if (total > 0 && idleDelta <= total) result = Math.Max(0, Math.Min(1, 1 - idleDelta / total));
            }
            idle = nextIdle; kernel = nextKernel; user = nextUser; primed = true;
            return result;
        }
    }

    public sealed class WindowsStateReader {
        private readonly CpuCounter cpu = new CpuCounter();
        public void Reset() { cpu.Reset(); }
        public SystemState Read() {
            SystemState result = new SystemState();
            Native.FileTime idle, kernel, user;
            if (Native.GetSystemTimes(out idle, out kernel, out user)) result.Cpu = cpu.Sample(idle.Value, kernel.Value, user.Value);
            else cpu.Reset();
            Native.PowerStatus power;
            if (Native.GetSystemPowerStatus(out power)) {
                SystemState decoded = DecodePower(power.ACLineStatus, power.BatteryFlag, power.BatteryLifePercent);
                result.OnAc = decoded.OnAc; result.Battery = decoded.Battery;
            }
            return result;
        }
        public static SystemState DecodePower(byte ac, byte flags, byte percent) {
            // 255 means unknown, not "charging" or "no battery".
            bool batteryPresent = flags != 255 && (flags & 128) == 0;
            return new SystemState {
                OnAc = ac == 1,
                Battery = batteryPresent && percent <= 100 ? (double?)(percent / 100.0) : null
            };
        }
    }
}
