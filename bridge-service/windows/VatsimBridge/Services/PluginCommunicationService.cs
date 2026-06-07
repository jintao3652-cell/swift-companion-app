using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using VatsimBridge.Hubs;
using VatsimBridge.Models;

namespace VatsimBridge.Services
{
    public interface IPluginCommunicationService
    {
        Task<OperationResult> SendMessageAsync(SendMessageRequest request);
        Task<OperationResult> ExecuteCommandAsync(string command);
        Task<AircraftStateDto> GetCurrentStateAsync();
        Task<bool> IsPluginConnectedAsync();
    }

    /// <summary>
    /// Service for communicating with vPilot plugin via HTTP
    /// </summary>
    public class PluginCommunicationService : IPluginCommunicationService
    {
        private readonly HttpClient _httpClient;
        private readonly IHubContext<VatsimHub> _hubContext;
        private readonly ILogger<PluginCommunicationService> _logger;
        private const string PLUGIN_BASE_URL = "http://localhost:8765";

        public PluginCommunicationService(
            HttpClient httpClient,
            IHubContext<VatsimHub> hubContext,
            ILogger<PluginCommunicationService> logger)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(PLUGIN_BASE_URL);
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task<bool> IsPluginConnectedAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/status");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<OperationResult> SendMessageAsync(SendMessageRequest request)
        {
            try
            {
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/send-message", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Message sent successfully: {request.MessageType}");
                    return OperationResult.Ok();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Failed to send message: {errorContent}");
                    return OperationResult.Failure($"Plugin returned error: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error communicating with plugin");
                return OperationResult.Failure("vPilot plugin is not running or not responding");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to plugin");
                return OperationResult.Failure($"Internal error: {ex.Message}");
            }
        }

        public async Task<OperationResult> ExecuteCommandAsync(string command)
        {
            try
            {
                var json = JsonSerializer.Serialize(new { command });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/execute-command", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Command executed successfully: {command}");
                    return OperationResult.Ok();
                }
                else
                {
                    return OperationResult.Failure("Failed to execute command");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing command");
                return OperationResult.Failure($"Internal error: {ex.Message}");
            }
        }

        public async Task<AircraftStateDto> GetCurrentStateAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/get-state");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var state = JsonSerializer.Deserialize<AircraftStateDto>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    return state ?? new AircraftStateDto();
                }
                else
                {
                    return new AircraftStateDto { Connected = false };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current state");
                return new AircraftStateDto { Connected = false };
            }
        }
    }
}
