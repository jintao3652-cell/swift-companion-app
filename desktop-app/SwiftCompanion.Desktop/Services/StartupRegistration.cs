using System.Diagnostics;
using Microsoft.Win32;

namespace SwiftCompanion.Desktop.Services;

/// <summary>
/// Registers Swift Companion to launch at Windows logon via the per-user Run key
/// (<c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>). No admin rights
/// required; the entry is cleanly removed on disable.
/// </summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SwiftCompanion";

    private static string ExePath =>
        Process.GetCurrentProcess().MainModule?.FileName
        ?? System.Reflection.Assembly.GetEntryAssembly()?.Location
        ?? "";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is not null;
        }
        catch { return false; }
    }

    public static void Enable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey);
        // Quote the path so spaces in the install dir are handled.
        key.SetValue(ValueName, $"\"{ExePath}\"");
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(ValueName) is not null)
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public static void Set(bool enabled)
    {
        if (enabled) Enable();
        else Disable();
    }
}
