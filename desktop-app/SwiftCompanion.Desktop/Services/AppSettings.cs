using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwiftCompanion.Desktop.Services;

/// <summary>What happens when the user clicks the window's close (X) button.</summary>
public enum CloseAction { Ask, HideToTray, Exit }

/// <summary>
/// User settings persisted to <c>%LocalAppData%\Swift Companion\settings.json</c>
/// (mirrors the reference app's per-user config location). Single instance,
/// loaded once at startup and saved on change.
/// </summary>
public sealed class AppSettings
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SwiftCompanion");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    public static AppSettings Instance { get; private set; } = new();

    /// <summary>vPilot plugin HTTP port the Bridge talks to. Default 8765.</summary>
    public int VpilotPort { get; set; } = 8765;

    /// <summary>Full path to the user's vPilot.exe. Empty until set; when set it
    /// overrides auto-detection so the plugin installs into the right folder.</summary>
    public string VpilotExePath { get; set; } = "";

    /// <summary>Whether Swift Companion launches at Windows logon (reflects the registry state).</summary>
    public bool RunAtStartup { get; set; }

    /// <summary>Set once the first-run vPilot welcome dialog has been shown, so we don't
    /// nag the user on every launch after they skipped it.</summary>
    public bool WelcomeShown { get; set; }

    /// <summary>What clicking the window close (X) button does. <see cref="CloseAction.Ask"/>
    /// pops the confirm dialog; the user can make it remember a choice. Serialized as a string
    /// ("Ask"/"HideToTray"/"Exit") so the JSON stays human-readable.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CloseAction CloseAction { get; set; } = CloseAction.Ask;

    // ===== 自建服务器中继（frp）=====

    /// <summary>Whether to expose the Bridge through the user's own frps server.</summary>
    public bool RelayEnabled { get; set; }

    /// <summary>frps server address (domain or IP), e.g. "bridge.example.com".</summary>
    public string RelayServerAddr { get; set; } = "";

    /// <summary>frps control port (bindPort on the server). Default 7000.</summary>
    public int RelayServerPort { get; set; } = 7000;

    /// <summary>Shared token that must match the server's auth.token.</summary>
    public string RelayToken { get; set; } = "";

    /// <summary>Public port on the server that forwards to the local Bridge. Default 15000.</summary>
    public int RelayRemotePort { get; set; } = 15000;

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded is not null) Instance = loaded;
            }
        }
        catch
        {
            // Corrupt/unreadable settings fall back to defaults.
            Instance = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Best effort; settings are non-critical.
        }
    }
}
