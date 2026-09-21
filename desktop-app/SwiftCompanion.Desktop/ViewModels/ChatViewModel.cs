using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SwiftCompanion.Desktop.Services;

namespace SwiftCompanion.Desktop.ViewModels;

/// <summary>One row in the chat list; isMine drives right-aligned bubble styling.</summary>
public partial class ChatRow : ObservableObject
{
    public ChatMessage Message { get; }
    public bool IsMine { get; }

    public ChatRow(ChatMessage message, string? ownCallsign)
    {
        Message = message;
        IsMine = message.From == "Me" ||
                 (!string.IsNullOrWhiteSpace(ownCallsign) && message.From == ownCallsign);
    }

    public string Sender => Message.From;
    public string Content => Message.Content;
    public string TimeText => Message.Timestamp.ToLocalTime().ToString("HH:mm:ss");
    public string TypeTag => Message.MessageType == "private" ? "PM" : "FREQ";
    public Visibility ShowSender => IsMine ? Visibility.Collapsed : Visibility.Visible;
}

public partial class ChatViewModel : ObservableObject
{
    private readonly BridgeManager _bridge;
    private readonly ChatService _chat;

    public ObservableCollection<ChatRow> Messages { get; } = new();

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _statusColor = "#FF8A93A6";
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _inputText = "";
    [ObservableProperty] private string _recipient = "";
    [ObservableProperty] private bool _isPrivateMode;
    [ObservableProperty] private Visibility _recipientVisibility = Visibility.Collapsed;

    public ChatViewModel(BridgeManager bridge, ChatService chat)
    {
        _bridge = bridge;
        _chat = chat;

        _chat.StateChanged += (_, _) => Application.Current.Dispatcher.BeginInvoke(Refresh);
        _chat.MessageArrived += (m, _) => Application.Current.Dispatcher.BeginInvoke(() => AddMessage(m));
        _bridge.StateChanged += (_, _) => Application.Current.Dispatcher.BeginInvoke(async () => await SyncConnectionAsync());

        LocalizationService.Instance.LanguageChanged += (_, _) => Application.Current.Dispatcher.BeginInvoke(Refresh);

        Refresh();
        _ = SyncConnectionAsync();
    }

    /// <summary>Match the chat connection to the Bridge lifecycle (start/stop/restart).</summary>
    private async Task SyncConnectionAsync()
    {
        if (_bridge.State == BridgeState.Running)
        {
            if (_chat.State == ChatConnectionState.Disconnected)
                await _chat.ConnectAsync(_bridge.Port);
        }
        else if (_chat.State != ChatConnectionState.Disconnected)
        {
            await _chat.DisconnectAsync();
        }
        Refresh();
    }

    private void Refresh()
    {
        var loc = LocalizationService.Instance;
        StatusText = _chat.State switch
        {
            ChatConnectionState.Connected => loc["Chat.Online"],
            ChatConnectionState.Connecting => loc["Chat.Connecting"],
            _ => _bridge.State == BridgeState.Running ? loc["Chat.Connecting"] : loc["Chat.Offline"]
        };
        StatusColor = _chat.State switch
        {
            ChatConnectionState.Connected => "#FF22C55E",
            ChatConnectionState.Connecting => "#FFEF9F27",
            _ => "#FF8A93A6"
        };
        IsConnected = _chat.State == ChatConnectionState.Connected;
    }

    private readonly HashSet<string> _seenIds = new(StringComparer.Ordinal);

    private void AddMessage(ChatMessage message)
    {
        // Same-id duplicates (history reload after reconnect)...
        if (!string.IsNullOrEmpty(message.Id) && !_seenIds.Add(message.Id)) return;

        // ...or echo of optimistic sends (same sender+content within 10s).
        var isDup = Messages.Any(r =>
            r.Message.From == message.From &&
            r.Message.Content == message.Content &&
            Math.Abs((message.Timestamp - r.Message.Timestamp).TotalSeconds) < 10);
        if (isDup) return;

        Messages.Add(new ChatRow(message, _chat.OwnCallsign));
        Trim();
    }

    private void Trim()
    {
        while (Messages.Count > 500) Messages.RemoveAt(0);
    }

    partial void OnIsPrivateModeChanged(bool value) =>
        RecipientVisibility = value ? Visibility.Visible : Visibility.Collapsed;

    [RelayCommand]
    private async Task Send()
    {
        var text = InputText.Trim();
        if (text.Length == 0) return;

        var ok = IsPrivateMode
            ? await _chat.SendPrivateAsync(Recipient.Trim(), text)
            : await _chat.SendRadioAsync(text);

        if (!ok) return;

        // Optimistic local echo; the plugin's UI monitor echoes the same content and
        // AddMessage's 10s dedupe window collapses the two.
        AddMessage(new ChatMessage
        {
            MessageType = IsPrivateMode ? "private" : "radio",
            From = _chat.OwnCallsign ?? "Me",
            To = IsPrivateMode ? Recipient.Trim() : "",
            Content = text,
            Timestamp = DateTime.UtcNow
        });
        InputText = "";
    }

    [RelayCommand]
    private async Task Reload() => await _chat.LoadHistoryAsync();
}
