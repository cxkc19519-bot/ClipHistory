using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ClipHistory.App.Clipboard;

public sealed class ClipboardMonitor : IDisposable
{
    private const int ClipboardUpdateMessage = 0x031D;
    private readonly Window window;
    private readonly Action clipboardChanged;
    private HwndSource? source;
    private nint windowHandle;
    private bool disposed;

    public ClipboardMonitor(Window window, Action clipboardChanged)
    {
        this.window = window ?? throw new ArgumentNullException(nameof(window));
        this.clipboardChanged = clipboardChanged ?? throw new ArgumentNullException(nameof(clipboardChanged));
        window.SourceInitialized += OnSourceInitialized;
        window.Closed += OnWindowClosed;
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

        if (windowHandle != 0)
        {
            _ = RemoveClipboardFormatListener(windowHandle);
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs eventArgs)
    {
        windowHandle = new WindowInteropHelper(window).Handle;
        source = HwndSource.FromHwnd(windowHandle);
        source.AddHook(WindowProcedure);

        if (!AddClipboardFormatListener(windowHandle))
        {
            throw new InvalidOperationException("Windows rejected clipboard monitoring registration.");
        }
    }

    private void OnWindowClosed(object? sender, EventArgs eventArgs)
    {
        Dispose();
    }

    private nint WindowProcedure(
        nint hwnd,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (message == ClipboardUpdateMessage)
        {
            clipboardChanged();
        }

        return 0;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(nint hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(nint hwnd);
}
