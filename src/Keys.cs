using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HoopLandAccess
{
    /// <summary>Reads keyboard and Xbox controller directly from Windows, so it works whatever input
    /// system the game uses. Only reacts while Hoop Land is the window in front.</summary>
    internal static class Keys
    {
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputState { public uint Packet; public ushort Buttons; public byte LT, RT; public short LX, LY, RX, RY; }
        [DllImport("xinput1_4.dll")] private static extern int XInputGetState(int user, out XInputState state);

        // Xbox button bits
        public const ushort DpadUp = 0x1, DpadDown = 0x2, DpadLeft = 0x4, DpadRight = 0x8,
            Start = 0x10, View = 0x20, LeftStick = 0x40, RightStick = 0x80,
            LB = 0x100, RB = 0x200, A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000;
        // Triggers are analog; we treat "pressed past half" as a button.
        public const uint LT = 0x10000, RT = 0x20000;

        // Windows key codes we use
        public const int Backspace = 0x08, Tab = 0x09, Enter = 0x0D, Shift = 0x10, Escape = 0x1B, Space = 0x20,
            PageUp = 0x21, PageDown = 0x22, End = 0x23, Home = 0x24,
            Left = 0x25, Up = 0x26, Right = 0x27, Down = 0x28;
        public static int F(int n) => 0x6F + n;

        private static readonly int[] Watched =
        {
            Backspace, Tab, Enter, Shift, Escape, Space, PageUp, PageDown, End, Home, Left, Up, Right, Down,
            0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x7B
        };

        private static readonly bool[] _now = new bool[256], _before = new bool[256];
        private static readonly float[] _downSince = new float[256], _nextRepeat = new float[256];
        private static uint _padNow, _padBefore;
        private static readonly uint _myPid = (uint)Process.GetCurrentProcess().Id;
        private static float _time;
        public static bool GameFocused { get; private set; }

        /// <summary>Call once per frame before asking for presses.</summary>
        public static void Update(float time)
        {
            _time = time;
            GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
            GameFocused = pid == _myPid;

            foreach (int vk in Watched)
            {
                _before[vk] = _now[vk];
                _now[vk] = GameFocused && (GetAsyncKeyState(vk) & 0x8000) != 0;
                if (_now[vk] && !_before[vk]) { _downSince[vk] = time; _nextRepeat[vk] = time + 0.45f; }
            }

            _padBefore = _padNow;
            _padNow = 0;
            if (GameFocused)
            {
                try { if (XInputGetState(0, out XInputState s) == 0) { _padNow = s.Buttons; if (s.LT > 128) _padNow |= LT; if (s.RT > 128) _padNow |= RT; } } catch { }
            }
        }

        /// <summary>True on the frame the key goes down.</summary>
        public static bool Pressed(int vk) => _now[vk] && !_before[vk];
        public static bool Held(int vk) => _now[vk];

        /// <summary>Like Pressed, but repeats while held (for arrows in long lists).</summary>
        public static bool PressedOrRepeat(int vk)
        {
            if (Pressed(vk)) return true;
            if (_now[vk] && _time >= _nextRepeat[vk]) { _nextRepeat[vk] = _time + 0.12f; return true; }
            return false;
        }

        public static bool PadPressed(uint button) => (_padNow & button) != 0 && (_padBefore & button) == 0;
        public static bool PadHeld(uint button) => (_padNow & button) != 0;
    }
}

