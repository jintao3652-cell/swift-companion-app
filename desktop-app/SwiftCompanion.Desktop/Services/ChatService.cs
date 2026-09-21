using System.Net.Http;
using System.Text.Json;
using System.Windows;
using Microsoft.AspNetCore.SignalR.Client;

namespace SwiftCompanion.Desktop.Services;

/// <summary>A chat message as delivered by the Bridge (MessageDto).</summary>
public class ChatMessage
{
    public string Id { get; set; } = "";
    public string MessageType { get; set; } = "radio";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public enum ChatConnectionState { Disconnected, Connecting, Connected }

/// <summary>
/// Real-time chat client for the in-process Bridge: SignalR subscription to
/// ReceiveMessage plus REST history. The desktop app, the phone and the vPilot
/// window all share the same Bridge message store, so everything stays in sync.
/// </summary>
public sealed class ChatService : IAsyncDisposable
{
    private HubConnection? _hub;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private int _port;

    public ChatConnectionState State { get; private set; } = ChatConnectionState.Disconnected;
    /// <summary>Own callsign as reported by the plugin state (null when unknown/offline).</summary>
    public string? OwnCallsign { get; private set; }

    /// <summary>Raised on any state change; marshal to the UI thread by the caller.</summary>
    public event EventHandler? StateChanged;
    /// <summary>Raised for every message (live or history batch); marshal to the UI thread.</summary>
    public event Action<ChatMessage, bool>? MessageArrived; // (message, isHistoryBatch)

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task ConnectAsync(int port)
    {
        _port = port;
        if (State is ChatConnectionState.Connected or ChatConnectionState.Connecting) return;

        SetState(ChatConnectionState.Connecting);
        try
        {
            var hub = new HubConnectionBuilder()
                .WithUrl($"http://localhost:{port}/swifthub")
                .WithAutomaticReconnect(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15) })
                .Build();

            hub.On<object>("ReceiveMessage", payload => RaiseMessage(payload, false));
            hub.On<object>("StateUpdated", payload => ExtractCallsign(payload));
            hub.On<object>("AircraftStateUpdated", payload => ExtractCallsign(payload));

            hub.Closed += async ex =>
            {
                if (ex != null) System.Diagnostics.Debug.WriteLine($"Chat hub closed: {ex.Message}");
                SetState(ChatConnectionState.Disconnected);
                // Bridge restarts on the same port when settings change; try to follow along.
                await Task.Delay(3000);
                if (State == ChatConnectionState.Disconnected && _hub == hub)
                    await SafeStartAsync(hub);
            };
            hub.Reconnected += _ =>
            {
                SetState(ChatConnectionState.Connected);
                return Task.CompletedTask;
            };

            _hub = hub;
            await hub.StartAsync();
            SetState(ChatConnectionState.Connected);

            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Chat connect failed: {ex.Message}");
            SetState(ChatConnectionState.Disconnected);
        }
    }

    public async Task DisconnectAsync()
    {
        if (_hub is null) { SetState(ChatConnectionState.Disconnected); return; }
        var hub = _hub;
        _hub = null;
        try { await hub.DisposeAsync(); } catch { /* shutting down */ }
        SetState(ChatConnectionState.Disconnected);
    }

    /// <summary>REST history (newest first from the API) — emitted oldest first.</summary>
    public async Task LoadHistoryAsync()
    {
        try
        {
            var json = await _http.GetStringAsync($"http://localhost:{_port}/api/messages?limit=200");
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("messages", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return;

            var list = new List<ChatMessage>();
            foreach (var el in arr.EnumerateArray())
            {
                var m = DeserializeMessage(el);
                if (m != null) list.Add(m);
            }
            list.Reverse(); // newest-first → oldest-first
            foreach (var m in list) MessageArrived?.Invoke(m, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Chat history load failed: {ex.Message}");
        }
    }

    public async Task<bool> SendRadioAsync(string content) => await SendAsync("SendRadioMessage", new[] { content });

    public async Task<bool> SendPrivateAsync(string recipient, string content) =>
        await SendAsync("SendPrivateMessage", new[] { recipient, content });

    private async Task<bool> SendAsync(string method, object[] args)
    {
        if (State != ChatConnectionState.Connected || _hub is null) return false;
        try
        {
            await _hub.InvokeAsync(method, args);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Chat send failed: {ex.Message}");
            return false;
        }
    }

    private async Task SafeStartAsync(HubConnection hub)
    {
        try
        {
            await hub.StartAsync();
            SetState(ChatConnectionState.Connected);
            await LoadHistoryAsync();
        }
        catch { /* next Closed/retry cycle handles it */ }
    }

    private void RaiseMessage(object payload, bool history)
    {
        var m = DeserializeMessage(payload);
        if (m != null) MessageArrived?.Invoke(m, history);
    }

    private ChatMessage? DeserializeMessage(object payload)
    {
        try
        {
            if (payload is JsonElement el)
                return JsonSerializer.Deserialize<ChatMessage>(el.GetRawText(), JsonOpts);
            var json = JsonSerializer.Serialize(payload);
            return JsonSerializer.Deserialize<ChatMessage>(json, JsonOpts);
        }
        catch { return null; }
    }

    /// <summary>Pull the own callsign out of a state payload (AircraftStateDto shape).</summary>
    private void ExtractCallsign(object payload)
    {
        try
        {
            JsonElement el;
            if (payload is JsonElement je) el = je;
            else
            {
                using var doc = JsonDocument.Parse(JsonSerializer.Serialize(payload));
                el = doc.RootElement.Clone();
            }
            if (el.ValueKind == JsonValueKind.Object &&
                el.TryGetProperty("callsign", out var cs) && cs.ValueKind == JsonValueKind.String)
            {
                var value = cs.GetString();
                if (!string.IsNullOrWhiteSpace(value) && value != "N/A" && value != OwnCallsign)
                {
                    OwnCallsign = value;
                    StateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        catch { /* non-fatal */ }
    }

    private void SetState(ChatConnectionState state)
    {
        if (State == state) return;
        State = state;
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) StateChanged?.Invoke(this, EventArgs.Empty);
        else dispatcher.BeginInvoke(() => StateChanged?.Invoke(this, EventArgs.Empty));
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
