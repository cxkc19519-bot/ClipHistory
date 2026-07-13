using System.IO;
using Microsoft.Win32;

namespace ClipHistory.App.Integration;

public sealed class WindowsStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ClipHistory";
    private readonly string startupCommand;

    public WindowsStartupService(string startupCommand)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startupCommand);
        this.startupCommand = startupCommand;
    }

    public static WindowsStartupService ForCurrentProcess()
    {
        string processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The current process path is unavailable.");
        bool usesDotnetHost = string.Equals(
            Path.GetFileNameWithoutExtension(processPath),
            "dotnet",
            StringComparison.OrdinalIgnoreCase);
        string command;
        if (usesDotnetHost)
        {
            string assemblyPath = Path.Combine(AppContext.BaseDirectory, "ClipHistory.App.dll");
            command = $"\"{processPath}\" \"{assemblyPath}\"";
        }
        else
        {
            command = $"\"{processPath}\"";
        }
        return new WindowsStartupService(command);
    }

    public bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return string.Equals(key?.GetValue(ValueName) as string, startupCommand, StringComparison.Ordinal);
    }

    public void SetEnabled(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled)
        {
            key.SetValue(ValueName, startupCommand, RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
