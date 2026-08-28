using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Lada.Models;
using Lada.Native;
using Lada.Services;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Windows.System;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using WinRT;
using Rectangle = System.Drawing.Rectangle;

namespace Lada.Windows;

// A non-layered native companion HWND. The real Lada remains the existing
// per-pixel-alpha WPF window above it, while DWM owns the live desktop blur
// in this window. Keeping the two responsibilities separate avoids relying
// on undocumented Acrylic behaviour for WS_EX_LAYERED WPF windows.
internal sealed class AcrylicBackdropHost : IDisposable
{
    private const string WindowClassName = "Lada.AcrylicBackdropHost";
    private const uint WM_NCHITTEST = 0x0084;
    private const uint WM_MOUSEACTIVATE = 0x0021;
    private const int HTTRANSPARENT = -1;
    private const int MA_NOACTIVATE = 3;

    private static DispatcherQueueController? s_dispatcherQueueController;
    private static readonly WindowProcedure s_windowProcedure = WindowProc;
    private static readonly object s_windowClassLock = new();
    private static IntPtr s_moduleHandle;
    private static bool s_windowClassRegistered;

    private IntPtr _handle;
    private Compositor? _compositor;
    private DesktopWindowTarget? _desktopTarget;
    private DesktopAcrylicController? _acrylicController;
    private SystemBackdropConfiguration? _configuration;
    private bool _visible;

    public IntPtr Handle => _handle;
    public bool IsAvailable { get; private set; }
    public bool HasLiveMaterial { get; private set; }
    public event Action? MaterialStateChanged;

    public bool EnsureCreated(Rectangle bounds, AppTheme theme)
    {
        if (_handle != IntPtr.Zero)
            return IsAvailable;
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
            return false;

        try
        {
            EnsureWindowClass();
            _handle = CreateWindowEx(
                NativeMethods.WS_EX_TOOLWINDOW
                    | NativeMethods.WS_EX_NOACTIVATE
                    | NativeMethods.WS_EX_TRANSPARENT,
                WindowClassName,
                "Lada Acrylic Backdrop",
                NativeMethods.WS_POPUP,
                bounds.X,
                bounds.Y,
                Math.Max(bounds.Width, 1),
                Math.Max(bounds.Height, 1),
                IntPtr.Zero,
                IntPtr.Zero,
                s_moduleHandle,
                IntPtr.Zero);
            if (_handle == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            if (!EnsureDispatcherQueue())
            {
                Dispose();
                return false;
            }

            // This is the documented Win32 path for Desktop Acrylic. The
            // explicit configuration is what keeps the material active on a
            // companion window that intentionally never takes input focus.
            var useHostBackdropBrush = 1;
            var hostBackdropResult = NativeMethods.DwmSetWindowAttribute(
                _handle,
                NativeMethods.DWMWA_USE_HOSTBACKDROPBRUSH,
                ref useHostBackdropBrush,
                sizeof(int));
            var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            var frameResult = NativeMethods.DwmExtendFrameIntoClientArea(_handle, ref margins);

            _compositor = new Compositor();
            var desktopInterop = _compositor.As<ICompositorDesktopInterop>();
            var targetResult = desktopInterop.CreateDesktopWindowTarget(
                _handle,
                true,
                out var targetPointer);
            if (targetResult < 0 || targetPointer == IntPtr.Zero)
                Marshal.ThrowExceptionForHR(targetResult);

            try
            {
                _desktopTarget = DesktopWindowTarget.FromAbi(targetPointer);
            }
            finally
            {
                Marshal.Release(targetPointer);
            }
            _desktopTarget.Root = _compositor.CreateContainerVisual();

            _configuration = new SystemBackdropConfiguration
            {
                IsInputActive = true,
                IsHighContrast = false
            };
            _acrylicController = new DesktopAcrylicController
            {
                Kind = DesktopAcrylicKind.Thin
            };
            _acrylicController.StateChanged += (_, _) => UpdateMaterialState();
            _acrylicController.SetSystemBackdropConfiguration(_configuration);
            var windowId = new WindowId(unchecked((ulong)_handle.ToInt64()));
            var backdropAttached = _acrylicController.SetTarget(windowId, _desktopTarget);

            if (hostBackdropResult < 0 || frameResult < 0 || !backdropAttached)
            {
                Dispose();
                return false;
            }

            var corners = NativeMethods.DWMWCP_ROUND;
            NativeMethods.DwmSetWindowAttribute(
                _handle,
                NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                ref corners,
                sizeof(int));
            var border = NativeMethods.DWM_COLOR_NONE;
            NativeMethods.DwmSetWindowAttribute(
                _handle,
                NativeMethods.DWMWA_BORDER_COLOR,
                ref border,
                sizeof(int));
            UpdateTheme(theme);

            IsAvailable = true;
            UpdateMaterialState();
            UpdateBounds(bounds);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Acrylic backdrop creation", ex);
            Dispose();
            return false;
        }
    }

    public void UpdateTheme(AppTheme theme)
    {
        if (Handle == IntPtr.Zero)
            return;

        var dark = theme == AppTheme.Modernism ? 0 : 1;
        NativeMethods.DwmSetWindowAttribute(
            Handle,
            NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE,
            ref dark,
            sizeof(int));

        if (_configuration is not null)
            _configuration.Theme = dark != 0 ? SystemBackdropTheme.Dark : SystemBackdropTheme.Light;
        if (_acrylicController is not null)
        {
            _acrylicController.TintColor = dark != 0
                ? global::Windows.UI.Color.FromArgb(255, 16, 24, 34)
                : global::Windows.UI.Color.FromArgb(255, 238, 244, 246);
            _acrylicController.TintOpacity = dark != 0 ? 0.20f : 0.30f;
            _acrylicController.LuminosityOpacity = dark != 0 ? 0.38f : 0.52f;
            _acrylicController.FallbackColor = dark != 0
                ? global::Windows.UI.Color.FromArgb(255, 25, 31, 39)
                : global::Windows.UI.Color.FromArgb(255, 229, 235, 238);
        }
    }

    public void UpdateBounds(Rectangle bounds)
    {
        if (Handle == IntPtr.Zero)
            return;

        NativeMethods.SetWindowPos(
            Handle,
            IntPtr.Zero,
            bounds.X,
            bounds.Y,
            Math.Max(bounds.Width, 1),
            Math.Max(bounds.Height, 1),
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    public void Show()
    {
        if (Handle == IntPtr.Zero || _visible)
            return;

        NativeMethods.ShowWindow(Handle, NativeMethods.SW_SHOWNOACTIVATE);
        _visible = true;
    }

    public void Hide()
    {
        if (Handle == IntPtr.Zero || !_visible)
            return;

        NativeMethods.ShowWindow(Handle, NativeMethods.SW_HIDE);
        _visible = false;
    }

    public void Dispose()
    {
        _visible = false;
        IsAvailable = false;
        HasLiveMaterial = false;
        _acrylicController?.Dispose();
        _acrylicController = null;
        _configuration = null;
        _desktopTarget?.Dispose();
        _desktopTarget = null;
        _compositor?.Dispose();
        _compositor = null;
        if (_handle != IntPtr.Zero)
        {
            DestroyWindow(_handle);
            _handle = IntPtr.Zero;
        }
    }

    private void UpdateMaterialState()
    {
        var isLive = _acrylicController?.State == SystemBackdropState.Active;
        if (HasLiveMaterial == isLive)
            return;

        HasLiveMaterial = isLive;
        MaterialStateChanged?.Invoke();
    }

    private static bool EnsureDispatcherQueue()
    {
        if (DispatcherQueue.GetForCurrentThread() is not null)
            return true;

        var options = new DispatcherQueueOptions
        {
            Size = Marshal.SizeOf<DispatcherQueueOptions>(),
            ThreadType = 2, // DQTYPE_THREAD_CURRENT
            ApartmentType = 0 // DQTAT_COM_NONE
        };
        var result = CreateDispatcherQueueController(options, out var controllerPointer);
        if (result < 0 || controllerPointer == IntPtr.Zero)
            return false;

        try
        {
            s_dispatcherQueueController = DispatcherQueueController.FromAbi(controllerPointer);
            return s_dispatcherQueueController is not null;
        }
        finally
        {
            Marshal.Release(controllerPointer);
        }
    }

    private static void EnsureWindowClass()
    {
        if (s_windowClassRegistered)
            return;

        lock (s_windowClassLock)
        {
            if (s_windowClassRegistered)
                return;

            s_moduleHandle = GetModuleHandle(null);
            var windowClass = new WindowClass
            {
                Size = (uint)Marshal.SizeOf<WindowClass>(),
                WindowProcedure = Marshal.GetFunctionPointerForDelegate(s_windowProcedure),
                Instance = s_moduleHandle,
                ClassName = WindowClassName
            };

            if (RegisterClassEx(ref windowClass) == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            s_windowClassRegistered = true;
        }
    }

    private static IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        return message switch
        {
            WM_NCHITTEST => new IntPtr(HTTRANSPARENT),
            WM_MOUSEACTIVATE => new IntPtr(MA_NOACTIVATE),
            _ => DefWindowProc(hwnd, message, wParam, lParam)
        };
    }

    [DllImport("CoreMessaging.dll")]
    private static extern int CreateDispatcherQueueController(
        DispatcherQueueOptions options,
        out IntPtr dispatcherQueueController);

    [StructLayout(LayoutKind.Sequential)]
    private struct DispatcherQueueOptions
    {
        public int Size;
        public int ThreadType;
        public int ApartmentType;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size;
        public uint Style;
        public IntPtr WindowProcedure;
        public int ClassExtraBytes;
        public int WindowExtraBytes;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr BackgroundBrush;
        [MarshalAs(UnmanagedType.LPWStr)] public string? MenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string ClassName;
        public IntPtr SmallIcon;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WindowClass windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        int extendedStyle,
        string className,
        string windowName,
        int style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [ComImport]
    [Guid("29E691FA-4567-4DCA-B319-D0F207EB6807")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICompositorDesktopInterop
    {
        [PreserveSig]
        int CreateDesktopWindowTarget(
            IntPtr hwndTarget,
            [MarshalAs(UnmanagedType.Bool)] bool isTopmost,
            out IntPtr result);

        [PreserveSig]
        int EnsureOnThread(uint threadId);
    }
}
