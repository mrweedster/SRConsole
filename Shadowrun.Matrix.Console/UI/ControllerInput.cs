using System.Runtime.InteropServices;

namespace Shadowrun.Matrix.UI;

/// <summary>
/// Unified controller input with three backends:
///
///   • XInput        — Xbox 360 / One / Series on Windows
///   • DualSense HID — PS5 DualSense on Windows (no DS4Windows required)
///   • SDL2          — Xbox + DualSense on Linux and macOS
///
/// Button mapping (all controllers):
///   D-Pad Up / Down / Left / Right → ↑ ↓ ← → arrow keys
///   A / Cross    → Enter
///   B / Circle   → Backspace
///   Y / Triangle → Escape
///
/// All presses are edge-triggered (no auto-repeat on hold).
/// Gracefully degrades to no-op if no controller library is found.
/// </summary>
public static class ControllerInput
{
    // =========================================================================
    // PUBLIC API
    // =========================================================================

    /// <summary>
    /// Call once per frame. Returns the first newly-pressed button as a
    /// synthetic ConsoleKeyInfo, or null when nothing is newly pressed.
    /// </summary>
    public static ConsoleKeyInfo? Poll()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var xi = PollXInput();
            if (xi.HasValue) return xi;
            var ds = PollDualSense();
            if (ds.HasValue) return ds;
            // SDL2 fallback on Windows: catches PS5/DualSense when the raw HID path
            // fails (e.g. Steam holds exclusive access). Steam ships SDL2 on Windows.
            return PollSdl2();
        }

        // Linux / macOS — SDL2 backend
        return PollSdl2();
    }

    // =========================================================================
    // XINPUT  (Windows — Xbox 360 / One / Series)
    // =========================================================================

    private const ushort XI_DPAD_UP    = 0x0001;
    private const ushort XI_DPAD_DOWN  = 0x0002;
    private const ushort XI_DPAD_LEFT  = 0x0004;
    private const ushort XI_DPAD_RIGHT = 0x0008;
    private const ushort XI_A          = 0x1000;
    private const ushort XI_B          = 0x2000;
    private const ushort XI_Y          = 0x8000;

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE
    {
        public uint   dwPacketNumber;
        public ushort wButtons;
        public byte   bLeftTrigger, bRightTrigger;
        public short  sThumbLX, sThumbLY, sThumbRX, sThumbRY;
    }

    [DllImport("XInput1_4.dll",   EntryPoint = "XInputGetState")] private static extern uint XI14(uint i, out XINPUT_STATE s);
    [DllImport("XInput9_1_0.dll", EntryPoint = "XInputGetState")] private static extern uint XI9 (uint i, out XINPUT_STATE s);

    private static bool              _xiChecked, _xiOk, _xiUse14;
    private static readonly ushort[] _xiPrev = new ushort[4];

    private static ConsoleKeyInfo? PollXInput()
    {
        if (!EnsureXInput()) return null;

        for (uint slot = 0; slot < 4; slot++)
        {
            uint hr = SafeXI(slot, out var st);
            if (hr != 0) { _xiPrev[slot] = 0; continue; }

            ushort curr    = st.wButtons;
            ushort pressed = (ushort)(curr & ~_xiPrev[slot]);
            _xiPrev[slot]  = curr;
            if (pressed == 0) continue;

            if ((pressed & XI_DPAD_UP)    != 0) return Syn('\0',   ConsoleKey.UpArrow);
            if ((pressed & XI_DPAD_DOWN)  != 0) return Syn('\0',   ConsoleKey.DownArrow);
            if ((pressed & XI_DPAD_LEFT)  != 0) return Syn('\0',   ConsoleKey.LeftArrow);
            if ((pressed & XI_DPAD_RIGHT) != 0) return Syn('\0',   ConsoleKey.RightArrow);
            if ((pressed & XI_A)          != 0) return Syn('\r',   ConsoleKey.Enter);
            if ((pressed & XI_B)          != 0) return Syn('\b',   ConsoleKey.Backspace);
            if ((pressed & XI_Y)          != 0) return Syn('\x1b', ConsoleKey.Escape);
        }
        return null;
    }

    private static bool EnsureXInput()
    {
        if (_xiChecked) return _xiOk;
        _xiChecked = true;
        try { _xiUse14 = true;  XI14(0, out _); return _xiOk = true; } catch { }
        try { _xiUse14 = false; XI9 (0, out _); return _xiOk = true; } catch { }
        return _xiOk = false;
    }

    private static uint SafeXI(uint slot, out XINPUT_STATE st)
    {
        try { return _xiUse14 ? XI14(slot, out st) : XI9(slot, out st); }
        catch { st = default; return 1; }
    }

    // =========================================================================
    // DUALSENSE HID  (Windows — PS5, no DS4Windows)
    // =========================================================================

    private const uint   DS_UP       = 1u << 0;
    private const uint   DS_DOWN     = 1u << 1;
    private const uint   DS_LEFT     = 1u << 2;
    private const uint   DS_RIGHT    = 1u << 3;
    private const uint   DS_CROSS    = 1u << 4;
    private const uint   DS_CIRCLE   = 1u << 5;
    private const uint   DS_TRIANGLE = 1u << 6;

    private const ushort SONY_VID           = 0x054C;
    private const ushort DUALSENSE_PID      = 0x0CE6;
    private const ushort DUALSENSE_EDGE_PID = 0x0DF2;
    private const byte   USB_REPORT         = 0x01;
    private const byte   BT_REPORT          = 0x31;

    [DllImport("hid.dll")]
    private static extern bool HidD_GetAttributes(IntPtr h, ref HID_ATTRIBS a);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr e, IntPtr hw, uint f);

    [DllImport("setupapi.dll")]
    private static extern bool SetupDiEnumDeviceInterfaces(IntPtr h, IntPtr d, ref Guid g, uint i, ref SP_DEV_IF_DATA data);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr h, ref SP_DEV_IF_DATA d, IntPtr det, uint sz, out uint req, IntPtr di);

    [DllImport("setupapi.dll")]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr h);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFile(string n, uint acc, uint sh, IntPtr sec, uint cr, uint fl, IntPtr tmpl);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(IntPtr h, byte[] buf, uint toRead, out uint read, IntPtr ov);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr h);

    [StructLayout(LayoutKind.Sequential)]
    private struct HID_ATTRIBS { public int Size; public ushort VendorId; public ushort ProductId; public ushort VersionNumber; }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEV_IF_DATA { public uint cbSize; public Guid InterfaceClassGuid; public uint Flags; public IntPtr Reserved; }

    private static readonly IntPtr INVALID_HANDLE       = new(-1);
    private static readonly Guid   HID_GUID             = new(0x4D1E55B2, 0xF16F, 0x11CF, 0x88, 0xCB, 0x00, 0x11, 0x11, 0x00, 0x00, 0x30);
    private const uint             DIGCF_PRESENT         = 0x02;
    private const uint             DIGCF_DEVICEINTERFACE = 0x10;
    private const uint             GENERIC_READ          = 0x80000000;
    private const uint             FILE_SHARE_RW         = 3;
    private const uint             OPEN_EXISTING         = 3;

    private static          IntPtr  _dsHandle    = new(-1);
    private static          Thread? _dsThread;
    private static volatile uint    _dsCurrent;
    private static          uint    _dsPrev;
    private static          long    _dsNextSearchMs;

    private static ConsoleKeyInfo? PollDualSense()
    {
        TryConnectDualSense();
        if (_dsHandle == INVALID_HANDLE) return null;

        uint curr    = _dsCurrent;
        uint pressed = curr & ~_dsPrev;
        _dsPrev      = curr;
        if (pressed == 0) return null;

        if ((pressed & DS_UP)       != 0) return Syn('\0',   ConsoleKey.UpArrow);
        if ((pressed & DS_DOWN)     != 0) return Syn('\0',   ConsoleKey.DownArrow);
        if ((pressed & DS_LEFT)     != 0) return Syn('\0',   ConsoleKey.LeftArrow);
        if ((pressed & DS_RIGHT)    != 0) return Syn('\0',   ConsoleKey.RightArrow);
        if ((pressed & DS_CROSS)    != 0) return Syn('\r',   ConsoleKey.Enter);
        if ((pressed & DS_CIRCLE)   != 0) return Syn('\b',   ConsoleKey.Backspace);
        if ((pressed & DS_TRIANGLE) != 0) return Syn('\x1b', ConsoleKey.Escape);
        return null;
    }

    private static void TryConnectDualSense()
    {
        if (_dsThread?.IsAlive == true && _dsHandle != INVALID_HANDLE) return;
        long now = Environment.TickCount64;
        if (now < _dsNextSearchMs) return;
        _dsNextSearchMs = now + 3_000;

        IntPtr h = FindAndOpenDualSense();
        if (h == INVALID_HANDLE) return;

        _dsHandle = h;
        _dsThread = new Thread(() => DsReadLoop(_dsHandle)) { IsBackground = true, Name = "DualSense-HID" };
        _dsThread.Start();
    }

    private static void DsReadLoop(IntPtr handle)
    {
        var buf = new byte[100];
        while (true)
        {
            bool ok = ReadFile(handle, buf, (uint)buf.Length, out uint read, IntPtr.Zero);
            if (!ok || read < 10)
            {
                CloseHandle(handle);
                if (_dsHandle == handle) _dsHandle = INVALID_HANDLE;
                _dsCurrent = 0;
                return;
            }
            if (buf[0] != USB_REPORT && buf[0] != BT_REPORT) continue;
            int o = buf[0] == BT_REPORT ? 1 : 0;
            if (read < (uint)(10 + o)) continue;

            byte b  = buf[8 + o];  // DualSense USB: buttons at byte 8; BT 0x31 adds 1 extra header byte
            int  dp = b & 0x0F;
            uint btns = 0;
            if (dp == 0 || dp == 1 || dp == 7) btns |= DS_UP;
            if (dp == 3 || dp == 4 || dp == 5) btns |= DS_DOWN;
            if (dp == 5 || dp == 6 || dp == 7) btns |= DS_LEFT;
            if (dp == 1 || dp == 2 || dp == 3) btns |= DS_RIGHT;
            if ((b & 0x20) != 0) btns |= DS_CROSS;
            if ((b & 0x40) != 0) btns |= DS_CIRCLE;
            if ((b & 0x80) != 0) btns |= DS_TRIANGLE;
            _dsCurrent = btns;
        }
    }

    private static IntPtr FindAndOpenDualSense()
    {
        try
        {
            var guid    = HID_GUID;
            IntPtr info = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (info == INVALID_HANDLE) return INVALID_HANDLE;
            try
            {
                for (uint i = 0; ; i++)
                {
                    var ifData = new SP_DEV_IF_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEV_IF_DATA>() };
                    if (!SetupDiEnumDeviceInterfaces(info, IntPtr.Zero, ref guid, i, ref ifData)) break;

                    SetupDiGetDeviceInterfaceDetail(info, ref ifData, IntPtr.Zero, 0, out uint needed, IntPtr.Zero);
                    if (needed == 0) continue;

                    IntPtr detailBuf = Marshal.AllocHGlobal((int)needed);
                    try
                    {
                        Marshal.WriteInt32(detailBuf, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetail(info, ref ifData, detailBuf, needed, out _, IntPtr.Zero)) continue;

                        string path = Marshal.PtrToStringUni(IntPtr.Add(detailBuf, 4)) ?? string.Empty;
                        if (path.Length == 0) continue;

                        IntPtr h = CreateFile(path, GENERIC_READ, FILE_SHARE_RW, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                        if (h == INVALID_HANDLE) continue;

                        var attribs = new HID_ATTRIBS { Size = Marshal.SizeOf<HID_ATTRIBS>() };
                        bool ok = HidD_GetAttributes(h, ref attribs)
                               && attribs.VendorId == SONY_VID
                               && (attribs.ProductId == DUALSENSE_PID || attribs.ProductId == DUALSENSE_EDGE_PID);
                        if (!ok) { CloseHandle(h); continue; }
                        return h;
                    }
                    finally { Marshal.FreeHGlobal(detailBuf); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(info); }
        }
        catch { /* HID / SetupAPI unavailable */ }
        return INVALID_HANDLE;
    }

    // =========================================================================
    // SDL2  (Linux / macOS — Xbox 360, Xbox One, DualShock 4, DualSense, etc.)
    //
    // Loaded at runtime via NativeLibrary so startup never fails if SDL2 is
    // absent; the backend simply stays disabled.
    //
    // SDL GameController button indices:
    //   SDL_CONTROLLER_BUTTON_A          = 0   (Xbox A  / PS Cross)    → Enter
    //   SDL_CONTROLLER_BUTTON_B          = 1   (Xbox B  / PS Circle)   → Backspace
    //   SDL_CONTROLLER_BUTTON_Y          = 3   (Xbox Y  / PS Triangle) → Escape
    //   SDL_CONTROLLER_BUTTON_DPAD_UP    = 11
    //   SDL_CONTROLLER_BUTTON_DPAD_DOWN  = 12
    //   SDL_CONTROLLER_BUTTON_DPAD_LEFT  = 13
    //   SDL_CONTROLLER_BUTTON_DPAD_RIGHT = 14
    //
    // Install on Linux:   sudo apt install libsdl2-dev   (or pacman -S sdl2, etc.)
    // Install on macOS:   brew install sdl2
    // =========================================================================

    private const int SDL_INIT_GAMECONTROLLER = 0x200;
    private const int SDL_BTN_A               = 0;
    private const int SDL_BTN_B               = 1;
    private const int SDL_BTN_Y               = 3;
    private const int SDL_BTN_DPAD_UP         = 11;
    private const int SDL_BTN_DPAD_DOWN       = 12;
    private const int SDL_BTN_DPAD_LEFT       = 13;
    private const int SDL_BTN_DPAD_RIGHT      = 14;

    private const uint SDL_M_UP    = 1u << 0;
    private const uint SDL_M_DOWN  = 1u << 1;
    private const uint SDL_M_LEFT  = 1u << 2;
    private const uint SDL_M_RIGHT = 1u << 3;
    private const uint SDL_M_A     = 1u << 4;
    private const uint SDL_M_B     = 1u << 5;
    private const uint SDL_M_Y     = 1u << 6;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int    D_SDL_Init(uint flags);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void   D_SDL_PumpEvents();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int    D_SDL_NumJoysticks();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte   D_SDL_IsGameController(int idx);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr D_SDL_GameControllerOpen(int idx);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void   D_SDL_GameControllerClose(IntPtr gc);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte   D_SDL_GameControllerGetButton(IntPtr gc, int btn);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int    D_SDL_GameControllerEventState(int state);

    private static bool                           _sdlChecked;
    private static bool                           _sdlOk;
    private static D_SDL_PumpEvents?              _sdlPump;
    private static D_SDL_NumJoysticks?            _sdlNumJoy;
    private static D_SDL_IsGameController?        _sdlIsGC;
    private static D_SDL_GameControllerOpen?      _sdlOpen;
    private static D_SDL_GameControllerClose?     _sdlClose;
    private static D_SDL_GameControllerGetButton? _sdlGetBtn;

    private static readonly IntPtr[] _sdlHandles     = new IntPtr[8];
    private static          uint     _sdlPrev;
    private static          long     _sdlNextRescanMs;

    private static ConsoleKeyInfo? PollSdl2()
    {
        if (!EnsureSdl2()) return null;

        // Pump SDL event queue so button states are current
        _sdlPump!();

        // Periodically rescan for newly connected / removed controllers
        long now = Environment.TickCount64;
        if (now >= _sdlNextRescanMs)
        {
            _sdlNextRescanMs = now + 3_000;
            RescanSdlControllers();
        }

        // Pack current state of all open controllers into a single bitmask
        uint curr = 0;
        foreach (IntPtr gc in _sdlHandles)
        {
            if (gc == IntPtr.Zero) continue;
            if (_sdlGetBtn!(gc, SDL_BTN_DPAD_UP)    != 0) curr |= SDL_M_UP;
            if (_sdlGetBtn( gc, SDL_BTN_DPAD_DOWN)  != 0) curr |= SDL_M_DOWN;
            if (_sdlGetBtn( gc, SDL_BTN_DPAD_LEFT)  != 0) curr |= SDL_M_LEFT;
            if (_sdlGetBtn( gc, SDL_BTN_DPAD_RIGHT) != 0) curr |= SDL_M_RIGHT;
            if (_sdlGetBtn( gc, SDL_BTN_A)          != 0) curr |= SDL_M_A;
            if (_sdlGetBtn( gc, SDL_BTN_B)          != 0) curr |= SDL_M_B;
            if (_sdlGetBtn( gc, SDL_BTN_Y)          != 0) curr |= SDL_M_Y;
        }

        uint pressed = curr & ~_sdlPrev;
        _sdlPrev = curr;
        if (pressed == 0) return null;

        if ((pressed & SDL_M_UP)    != 0) return Syn('\0',   ConsoleKey.UpArrow);
        if ((pressed & SDL_M_DOWN)  != 0) return Syn('\0',   ConsoleKey.DownArrow);
        if ((pressed & SDL_M_LEFT)  != 0) return Syn('\0',   ConsoleKey.LeftArrow);
        if ((pressed & SDL_M_RIGHT) != 0) return Syn('\0',   ConsoleKey.RightArrow);
        if ((pressed & SDL_M_A)     != 0) return Syn('\r',   ConsoleKey.Enter);
        if ((pressed & SDL_M_B)     != 0) return Syn('\b',   ConsoleKey.Backspace);
        if ((pressed & SDL_M_Y)     != 0) return Syn('\x1b', ConsoleKey.Escape);
        return null;
    }

    /// <summary>
    /// Lazy-loads SDL2. Tries platform-specific library names in order;
    /// returns false permanently if none are loadable.
    /// </summary>
    private static bool EnsureSdl2()
    {
        if (_sdlChecked) return _sdlOk;
        _sdlChecked = true;

        string[] candidates = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? [
                "libSDL2.dylib",
                "libSDL2-2.0.0.dylib",
                "/usr/local/lib/libSDL2.dylib",
                "/opt/homebrew/lib/libSDL2.dylib",
              ]
            : [ // Linux
                "libSDL2-2.0.so.0",
                "libSDL2.so",
                "libSDL2-2.0.so",
              ];

        IntPtr lib = IntPtr.Zero;
        foreach (string name in candidates)
            if (NativeLibrary.TryLoad(name, out lib)) break;

        if (lib == IntPtr.Zero) return _sdlOk = false;

        try
        {
            T Fn<T>(string fn) where T : Delegate =>
                Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(lib, fn));

            var init     = Fn<D_SDL_Init>("SDL_Init");
            _sdlPump     = Fn<D_SDL_PumpEvents>("SDL_PumpEvents");
            _sdlNumJoy   = Fn<D_SDL_NumJoysticks>("SDL_NumJoysticks");
            _sdlIsGC     = Fn<D_SDL_IsGameController>("SDL_IsGameController");
            _sdlOpen     = Fn<D_SDL_GameControllerOpen>("SDL_GameControllerOpen");
            _sdlClose    = Fn<D_SDL_GameControllerClose>("SDL_GameControllerClose");
            _sdlGetBtn   = Fn<D_SDL_GameControllerGetButton>("SDL_GameControllerGetButton");

            // Disable automatic game-controller events — we poll manually
            var evtState = Fn<D_SDL_GameControllerEventState>("SDL_GameControllerEventState");
            evtState(0);

            if (init((uint)SDL_INIT_GAMECONTROLLER) != 0) return _sdlOk = false;

            RescanSdlControllers();
            return _sdlOk = true;
        }
        catch
        {
            return _sdlOk = false;
        }
    }

    /// <summary>
    /// Opens newly connected joysticks that SDL recognises as game controllers
    /// and closes stale handles beyond the current joystick count.
    /// </summary>
    private static void RescanSdlControllers()
    {
        if (_sdlNumJoy == null || _sdlIsGC == null || _sdlOpen == null || _sdlClose == null)
            return;

        int count = _sdlNumJoy();
        int slot  = 0;

        for (int i = 0; i < count && slot < _sdlHandles.Length; i++)
        {
            if (_sdlIsGC(i) == 0) continue;
            if (_sdlHandles[slot] == IntPtr.Zero)
                _sdlHandles[slot] = _sdlOpen(i);
            slot++;
        }

        // Release handles for controllers no longer present
        for (int s = slot; s < _sdlHandles.Length; s++)
        {
            if (_sdlHandles[s] == IntPtr.Zero) continue;
            _sdlClose(_sdlHandles[s]);
            _sdlHandles[s] = IntPtr.Zero;
        }
    }

    // =========================================================================
    // SHARED HELPER
    // =========================================================================

    private static ConsoleKeyInfo Syn(char ch, ConsoleKey key) =>
        new(ch, key, shift: false, alt: false, control: false);
}
