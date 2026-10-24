using System.Runtime.InteropServices;

namespace PKForge.Linux.Native;

internal static class Sdl
{
    private const string Lib = "SDL2";
    static Sdl()
    {
        NativeLibrary.SetDllImportResolver(typeof(Sdl).Assembly, (name, assembly, path) =>
        {
            var library = name switch
            {
                "SDL2" when OperatingSystem.IsLinux() => "libSDL2-2.0.so.0",
                "SDL2_mixer" when OperatingSystem.IsLinux() => "libSDL2_mixer-2.0.so.0",
                _ => name
            };
            return NativeLibrary.TryLoad(library, assembly, path, out var handle) ? handle : IntPtr.Zero;
        });
    }
    public const uint InitVideo = 0x20, InitAudio = 0x10, InitController = 0x2000, InitEvents = 0x4000;
    public const uint WindowOpenGl = 2, WindowShown = 4, WindowResizable = 0x20, WindowBorderless = 0x10, WindowFullscreen = 0x1001;
    public const uint Quit = 0x100, WindowEvent = 0x200, KeyDown = 0x300, KeyUp = 0x301,
        MouseMotion = 0x400, MouseDown = 0x401, MouseUp = 0x402, MouseWheel = 0x403,
        ControllerAxis = 0x650, ControllerDown = 0x651, ControllerUp = 0x652, ControllerAdded = 0x653,
        ControllerRemoved = 0x654, FingerDown = 0x700, FingerUp = 0x701, FingerMotion = 0x702;
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_Init(uint flags);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_InitSubSystem(uint flags);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_GetNumAudioDrivers();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr SDL_GetAudioDriver(int index);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_setenv([MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, int overwrite);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_Quit();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_PollEvent(out Event value);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr SDL_GetError();
    public static string Error => Marshal.PtrToStringUTF8(SDL_GetError()) ?? "SDL error";
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_SetHint([MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr SDL_CreateWindow([MarshalAs(UnmanagedType.LPUTF8Str)] string title, int x, int y, int w, int h, uint flags);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_DestroyWindow(IntPtr window);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_ShowWindow(IntPtr window);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_HideWindow(IntPtr window);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern uint SDL_GetWindowID(IntPtr window);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_GetWindowSize(IntPtr window, out int width, out int height);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_GetNumVideoDisplays();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_GetDisplayBounds(int display, out Rect rect);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr SDL_GetCurrentVideoDriver();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_ShowCursor(int toggle);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_GL_SetAttribute(int attribute, int value);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr SDL_GL_CreateContext(IntPtr window);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_GL_MakeCurrent(IntPtr window, IntPtr context);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_GL_DeleteContext(IntPtr context);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_GL_GetDrawableSize(IntPtr window, out int width, out int height);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_GL_SwapWindow(IntPtr window);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_GL_SetSwapInterval(int interval);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr SDL_GL_GetProcAddress([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr SDL_CreateRenderer(IntPtr window, int index, uint flags);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_DestroyRenderer(IntPtr renderer);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr SDL_CreateTexture(IntPtr renderer, uint format, int access, int width, int height);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_DestroyTexture(IntPtr texture);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_UpdateTexture(IntPtr texture, IntPtr rect, IntPtr pixels, int pitch);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_RenderClear(IntPtr renderer);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_RenderCopy(IntPtr renderer, IntPtr texture, IntPtr src, IntPtr dst);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_RenderPresent(IntPtr renderer);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_NumJoysticks();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr SDL_JoystickNameForIndex(int index);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_IsGameController(int index);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr SDL_GameControllerOpen(int index);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_GameControllerClose(IntPtr controller);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr SDL_GameControllerName(IntPtr controller);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern short SDL_GameControllerGetAxis(IntPtr controller, int axis);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern byte SDL_GameControllerGetButton(IntPtr controller, int button);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_GameControllerAddMapping([MarshalAs(UnmanagedType.LPUTF8Str)] string mapping);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr SDL_RWFromFile([MarshalAs(UnmanagedType.LPUTF8Str)] string file, [MarshalAs(UnmanagedType.LPUTF8Str)] string mode);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int X, Y, W, H; }
    [StructLayout(LayoutKind.Explicit, Size = 56)] public struct Event
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public uint WindowId;
        [FieldOffset(12)] public byte WindowKind;
        [FieldOffset(13)] public byte KeyRepeat;
        [FieldOffset(20)] public int Key;
        [FieldOffset(12)] public byte Button;
        [FieldOffset(16)] public int MouseX;
        [FieldOffset(20)] public int MouseY;
        [FieldOffset(8)] public long TouchId;
        [FieldOffset(16)] public long FingerId;
        [FieldOffset(24)] public float FingerX;
        [FieldOffset(28)] public float FingerY;
        [FieldOffset(44)] public uint FingerWindowId;
    }
}
