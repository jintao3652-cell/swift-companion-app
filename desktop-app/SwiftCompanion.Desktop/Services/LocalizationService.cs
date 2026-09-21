using System.Globalization;
using System.Windows;

namespace SwiftCompanion.Desktop.Services;

public enum AppLanguage { English, Chinese }

/// <summary>
/// Runtime i18n: holds the active language and exposes localized strings as application
/// resources keyed "L.&lt;Key&gt;", so XAML can bind with {DynamicResource L.Xyz} and the UI
/// updates live when the language switches. First run follows the OS UI language.
/// </summary>
public sealed class LocalizationService
{
    public static LocalizationService Instance { get; } = new();

    public AppLanguage Current { get; private set; }
    public event EventHandler? LanguageChanged;

    private static readonly Dictionary<string, (string En, string Zh)> Strings = new()
    {
        ["App.Tagline"]        = ("VATSIM Companion Bridge", "VATSIM 伴侣桥接"),
        // Tabs
        ["Tab.Home"]           = ("Dashboard", "主页"),
        ["Tab.Pair"]           = ("Devices", "配对手机"),
        ["Tab.Chat"]           = ("Messages", "消息"),
        ["Tab.Settings"]       = ("Settings", "设置"),
        ["Tab.Tools"]          = ("Plugins", "工具"),
        // Home
        ["Home.Title"]         = ("Dashboard", "仪表盘"),
        ["Home.Subtitle"]      = ("Service status and connection details", "桥接服务状态与连接信息"),
        ["Home.BridgeService"] = ("BRIDGE SERVICE", "桥接服务"),
        ["Home.ConnInfo"]      = ("CONNECTION INFO", "连接信息"),
        ["Home.LocalUrl"]      = ("Local URL", "本地地址"),
        ["Home.NetworkUrl"]    = ("Network URL", "局域网地址"),
        ["Home.PublicUrl"]     = ("Public URL", "公网地址"),
        ["Home.Tip"]           = ("Tip: head to the Devices tab to connect your phone.",
                                  "提示：打开“配对手机”标签页来连接你的设备。"),
        // Status
        ["Status.Running"]     = ("Running on port {0}", "运行中 · 端口 {0}"),
        ["Status.Starting"]    = ("Starting…", "启动中…"),
        ["Status.Stopped"]     = ("Stopped", "已停止"),
        ["Status.Error"]       = ("Error: {0}", "错误：{0}"),
        // Buttons
        ["Btn.Start"]          = ("Start", "启动"),
        ["Btn.Stop"]           = ("Stop", "停止"),
        ["Btn.Restart"]        = ("Restart", "重启"),
        ["Btn.Copy"]           = ("Copy", "复制"),
        ["Btn.Regenerate"]     = ("Regenerate", "重新生成"),
        ["Btn.NewBridge"]      = ("New bridge", "重建桥接"),
        ["Btn.NewBridge.Tip"]  = ("Restart the bridge on a new port and issue a fresh pairing code. Connected phones must re-pair.",
                                  "在新端口重启桥接并生成全新配对码。已连接的手机需要重新配对。"),
        // Pair
        ["Pair.Title"]         = ("Devices", "配对手机"),
        ["Pair.Subtitle"]      = ("Scan this QR code in the mobile app to connect.",
                                  "用手机 App 扫描二维码即可连接。"),
        ["Pair.Code"]          = ("Pairing code", "配对码"),
        ["Pair.Waiting"]       = ("Generating…", "生成中…"),
        ["Pair.NeedRunning"]   = ("Start the service first from the Dashboard.", "请先在主页启动桥接服务。"),
        // Tunnel
        ["Tunnel.Title"]       = ("PUBLIC ACCESS (CLOUDFLARE TUNNEL)", "公网访问（CLOUDFLARE 隧道）"),
        ["Tunnel.Desc"]        = ("Reach the service over the internet so your phone can connect from anywhere.",
                                  "通过公网暴露桥接服务，让手机在任意网络下都能连接。"),
        ["Tunnel.Enable"]      = ("Enable tunnel", "启用隧道"),
        ["Tunnel.Starting"]    = ("Starting tunnel…", "隧道启动中…"),
        ["Tunnel.Running"]     = ("Tunnel active", "隧道已激活"),
        ["Tunnel.Stopped"]     = ("Tunnel off", "隧道关闭"),
        ["Tunnel.Error"]       = ("Tunnel failed: {0}", "隧道失败：{0}"),
        ["Tunnel.NotFound"]    = ("cloudflared.exe not found.", "未找到 cloudflared.exe。"),
        ["Tunnel.Copy"]        = ("Copy link", "复制链接"),
        ["Tunnel.Copied"]      = ("Copied!", "已复制！"),
        // Language
        ["Lang.Label"]         = ("Language", "语言"),
        // Chat (Messages tab)
        ["Chat.Title"]         = ("Messages", "消息"),
        ["Chat.Subtitle"]      = ("Live feed shared with the phone and vPilot — radio and private messages.",
                                  "与手机和 vPilot 实时同步的消息流——频率消息与私信。"),
        ["Chat.Online"]        = ("Connected — messages sync live with your phone", "已连接——与手机实时同步"),
        ["Chat.Connecting"]    = ("Connecting…", "连接中…"),
        ["Chat.Offline"]       = ("Bridge not running — start it from the Dashboard.", "桥接未运行——请先在主页启动。"),
        ["Chat.Reload"]        = ("Reload history", "刷新历史"),
        ["Chat.ModeRadio"]     = ("Frequency", "频率"),
        ["Chat.ModePrivate"]   = ("Private", "私信"),
        ["Chat.RecipientHint"] = ("Recipient callsign, e.g. GND", "对方呼号，如 GND"),

        // Settings
        ["Settings.Title"]     = ("Settings", "设置"),
        ["Settings.Subtitle"]  = ("Configure the vPilot connection and startup behavior.",
                                  "配置 vPilot 连接与启动行为。"),
        ["Settings.Vpilot"]    = ("VPILOT CONNECTION", "VPILOT 连接"),
        ["Settings.VpilotPort"]= ("Plugin port", "插件端口"),
        ["Settings.VpilotPath"]= ("vPilot.exe path", "vPilot.exe 路径"),
        ["Settings.Browse"]    = ("Browse…", "浏览…"),
        ["Settings.PathHint"]  = ("Where vPilot.exe is installed. Used to install the plugin.",
                                  "vPilot.exe 的安装位置，用于安装插件。"),
        ["Settings.PathBad"]   = ("Select a valid vPilot.exe file.", "请选择有效的 vPilot.exe 文件。"),
        ["Settings.PickVpilot"]= ("Locate vPilot.exe", "定位 vPilot.exe"),
        ["Settings.PortHint"]  = ("Port the vPilot plugin listens on (default 8765).",
                                  "vPilot 插件监听的 HTTP 端口（默认 8765）。"),
        ["Settings.PortBad"]   = ("Enter a port from 1 to 65535.", "请输入 1–65535 之间的端口。"),
        ["Settings.Saved"]     = ("Saved — applied right away.", "已保存，立即生效。"),
        ["Settings.General"]   = ("GENERAL", "常规"),
        ["Settings.RunStartup"]= ("Start Swift Companion when Windows starts", "随 Windows 启动 Swift Companion"),
        ["Btn.Save"]           = ("Save", "保存"),
        ["Btn.Send"]           = ("Send", "发送"),
        // Tools
        ["Tools.Title"]        = ("Plugins", "工具"),
        ["Tools.Subtitle"]     = ("Set up and manage the vPilot plugin.", "安装与管理 vPilot 插件。"),
        ["Tools.Plugin"]       = ("VPILOT PLUGIN", "VPILOT 插件"),
        ["Tools.PluginDesc"]   = ("Install the companion plugin into vPilot so it can relay messages.",
                                  "将伴侣插件安装到 vPilot，使其能转发消息。"),
        ["Tools.Detected"]     = ("Found vPilot", "检测到 vPilot"),
        ["Tools.NotDetected"]  = ("vPilot not found", "未检测到 vPilot"),
        ["Tools.Install"]      = ("Install plugin", "安装插件"),
        ["Tools.Ok.Installed"] = ("Installed to {0}", "已安装到 {0}"),
        ["Tools.Err.NoBundle"] = ("Bundled plugin file is missing from the app folder.",
                                  "应用目录中缺少打包的插件 DLL。"),
        ["Tools.Err.NoVpilot"] = ("Couldn't find vPilot. Install vPilot first.",
                                  "未找到 vPilot，请先安装 vPilot。"),
        ["Tools.Err.Copy"]     = ("Couldn't copy the plugin: {0}", "复制插件失败：{0}"),
        // First-run welcome
        ["Welcome.Title"]      = ("Welcome to Swift Companion", "欢迎使用 Swift Companion"),
        ["Welcome.Body"]       = ("We couldn't find vPilot automatically. Please locate your vPilot.exe so Swift Companion can install the companion plugin.",
                                  "未能自动找到 vPilot。请定位你的 vPilot.exe，以便 Swift Companion 安装伴侣插件。"),
        ["Welcome.Browse"]     = ("Locate vPilot.exe…", "定位 vPilot.exe…"),
        ["Welcome.Skip"]       = ("Skip for now", "暂时跳过"),
        ["Welcome.Selected"]   = ("Selected: {0}", "已选择：{0}"),
        // Close-to-tray dialog
        ["Close.Title"]        = ("Close Swift Companion?", "关闭 Swift Companion？"),
        ["Close.Body"]         = ("Keep Swift Companion running in the background so your phone stays connected, or exit completely and stop the bridge service.",
                                  "可以让 Swift Companion 在后台继续运行，保持手机连接；也可以完全退出并停止桥接服务。"),
        ["Close.HideToTray"]   = ("Minimize to tray", "隐藏到托盘"),
        ["Close.Exit"]         = ("Exit completely", "完全退出"),
        ["Close.Remember"]     = ("Remember my choice", "记住此决定"),
        // System tray
        ["Tray.Tooltip"]       = ("Swift Companion — running in background", "Swift Companion — 正在后台运行"),
        ["Tray.Show"]          = ("Show Swift Companion", "显示 Swift Companion"),
        ["Tray.Exit"]          = ("Exit", "退出"),
        ["Tray.Hidden.Title"]  = ("Swift Companion is still running", "Swift Companion 仍在后台运行"),
        ["Tray.Hidden.Body"]   = ("The bridge keeps running in the tray. Double-click the icon to reopen.",
                                  "桥接服务在托盘中继续运行。双击托盘图标可重新打开。"),
        // Settings — close behavior
        ["Settings.CloseBehavior"]   = ("When closing the window", "点击关闭按钮时"),
        ["Settings.Close.Ask"]       = ("Always ask", "每次询问"),
        ["Settings.Close.HideToTray"]= ("Minimize to tray", "隐藏到托盘"),
        ["Settings.Close.Exit"]      = ("Exit completely", "完全退出"),
        // Settings — self-hosted relay (frp)
        ["Settings.Relay"]           = ("SELF-HOSTED RELAY (CHINA / OWN SERVER)", "自建服务器中继（国内直连）"),
        ["Settings.Relay.Desc"]      = ("Expose the Bridge through your own frps server. Phones connect to <server>:<public port>. Recommended for China, more stable than the Cloudflare tunnel.",
                                        "通过你自己的 frps 服务器转发 Bridge，手机连接「服务器地址:公网端口」。国内网络下比 Cloudflare 隧道更稳定。"),
        ["Settings.Relay.Enable"]    = ("Enable relay", "启用中继"),
        ["Settings.Relay.Server"]    = ("Server address", "服务器地址"),
        ["Settings.Relay.ServerPort"]= ("Server port", "服务端口"),
        ["Settings.Relay.Token"]     = ("Token", "鉴权 Token"),
        ["Settings.Relay.RemotePort"]= ("Public port", "公网端口"),
        ["Settings.Relay.Apply"]     = ("Apply relay settings", "应用中继设置"),
        ["Settings.Relay.RunningFmt"]= ("Relay active — phone connects to {0}", "中继已激活 — 手机连接 {0}"),
        ["Settings.Relay.Starting"]  = ("Connecting to relay server…", "正在连接中继服务器…"),
        ["Settings.Relay.Stopped"]   = ("Relay off", "中继关闭"),
        ["Settings.Relay.ErrorFmt"]  = ("Relay failed: {0}", "中继失败：{0}"),
        ["Settings.Relay.NeedAddr"]  = ("Enter your server address first.", "请先填写服务器地址。"),
        ["Settings.Relay.Missing"]   = ("frpc.exe is not bundled in this install.", "当前安装缺少 frpc.exe。"),
    };

    private LocalizationService()
    {
        // Follow OS UI language on first run.
        var ui = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        Current = ui.Equals("zh", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.Chinese : AppLanguage.English;
    }

    public string this[string key] =>
        Strings.TryGetValue(key, out var v)
            ? (Current == AppLanguage.Chinese ? v.Zh : v.En)
            : key;

    /// <summary>Format a localized template, e.g. Format("Status.Running", port).</summary>
    public string Format(string key, params object[] args) => string.Format(this[key], args);

    /// <summary>Push every string into Application.Resources under "L.&lt;Key&gt;".</summary>
    public void Apply()
    {
        var res = Application.Current.Resources;
        foreach (var key in Strings.Keys)
            res["L." + key] = this[key];
    }

    public void SetLanguage(AppLanguage lang)
    {
        if (lang == Current) return;
        Current = lang;
        Apply();
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle() =>
        SetLanguage(Current == AppLanguage.English ? AppLanguage.Chinese : AppLanguage.English);
}
