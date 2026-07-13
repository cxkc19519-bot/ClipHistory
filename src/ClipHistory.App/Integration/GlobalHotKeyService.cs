using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ClipHistory.Infrastructure.Settings;

namespace ClipHistory.App.Integration;

public sealed class GlobalHotKeyService : IDisposable
{
    private const int HotKeyMessage = 0x0312;
    private const int HotKeyId = 0x4348;
    private const uint ControlModifier = 0x0002;
    private const uint ShiftModifier = 0x0004;
    private const uint AltModifier = 0x0001;
    private const uint VirtualKeyV = 0x56;
    private const uint VirtualKeyC = 0x43;

    private readonly Window window;
    private readonly Action hotKeyPressed;
    private readonly Action registrationFailed;
    private HwndSource? source;
    private nint windowHandle;
    private bool registered;
    private bool disposed;
    private HotKeyOption currentOption;

    public GlobalHotKeyService(
        Window window,
        Action hotKeyPressed,
        Action registrationFailed,
        HotKeyOption initialOption)
    {
        this.window = window ?? throw new ArgumentNullException(nameof(window));
        this.hotKeyPressed = hotKeyPressed ?? throw new ArgumentNullException(nameof(hotKeyPressed));
        this.registrationFailed = registrationFailed ?? throw new ArgumentNullException(nameof(registrationFailed));
        currentOption = initialOption;
        window.SourceInitialized += OnSourceInitialized;
        window.Closed += OnWindowClosed;
    }

    public bool TryChange(HotKeyOption option)
    {
        if (!Enum.IsDefined(option))
        {
            throw new ArgumentOutOfRangeException(nameof(option));
        }

        if (option == currentOption)
        {
            return registered || windowHandle == 0;
        }

        HotKeyOption previous = currentOption;
        if (registered)
        {
            _ = UnregisterHotKey(windowHandle, HotKeyId);
            registered = false;
        }

        currentOption = option;
        registered = windowHandle == 0 || Register(option);
        if (registered)
        {
            return true;
        }

        currentOption = previous;
        registered = Register(previous);
        registrationFailed();
        return false;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        window.SourceInitialized -= OnSourceInitialized;
        window.Closed -= OnWindowClosed;

        if (source is not null)
        {
            source.RemoveHook(WindowProcedure);
        }

        if (registered && windowHandle != 0)
        {
            _ = UnregisterHotKey(windowHandle, HotKeyId);
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        windowHandle = new WindowInteropHelper(window).Handle;
        source = HwndSource.FromHwnd(windowHandle);
        source.AddHook(WindowProcedure);
        registered = Register(currentOption);

        if (!registered)
        {
            registrationFailed();
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e) => Dispose();

    private bool Register(HotKeyOption option)
    {
        (uint modifiers, uint virtualKey) = option switch
        {
            HotKeyOption.ControlAltV => (ControlModifier | AltModifier, VirtualKeyV),
            HotKeyOption.ControlShiftC => (ControlModifier | ShiftModifier, VirtualKeyC),
            _ => (ControlModifier | ShiftModifier, VirtualKeyV),
        };
        return RegisterHotKey(windowHandle, HotKeyId, modifiers, virtualKey);
    }

    private nint WindowProcedure(
        nint hwnd,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (message == HotKeyMessage && wParam == HotKeyId)
        {
            hotKeyPressed();
            handled = true;
        }

        return 0;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hwnd, int id);
}
