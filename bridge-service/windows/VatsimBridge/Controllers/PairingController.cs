using Microsoft.AspNetCore.Mvc;
using VatsimBridge.Models;
using VatsimBridge.Services;
using QRCoder;

namespace VatsimBridge.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PairingController : ControllerBase
    {
        private readonly IPairingService _pairingService;
        private readonly IPushNotificationService _pushService;
        private readonly ILogger<PairingController> _logger;
        private readonly IConfiguration _configuration;

        public PairingController(
            IPairingService pairingService,
            IPushNotificationService pushService,
            ILogger<PairingController> logger,
            IConfiguration configuration)
        {
            _pairingService = pairingService;
            _pushService = pushService;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Start pairing process - generate pairing code
        /// </summary>
        [HttpPost("start")]
        public IActionResult StartPairing()
        {
            try
            {
                var pairingCode = _pairingService.GeneratePairingCode();
                // 隧道场景: bat 把 trycloudflare 公网地址写入 PublicUrl, 手机扫码才能连上
                var bridgeUrl = _configuration["PublicUrl"]
                    ?? $"http://localhost:{_configuration["Port"] ?? "5000"}";

                // Generate QR code containing pairing info
                var qrData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    bridgeUrl = bridgeUrl,
                    pairingCode = pairingCode,
                    timestamp = DateTime.UtcNow
                });

                string qrCodeBase64 = GenerateQRCode(qrData);

                var response = new
                {
                    success = true,
                    pairingCode = pairingCode,
                    qrCode = qrCodeBase64,
                    bridgeUrl = bridgeUrl,
                    expiresAt = DateTime.UtcNow.AddMinutes(10)
                };

                _logger.LogInformation("===========================================");
                _logger.LogInformation("PAIRING CODE GENERATED");
                _logger.LogInformation($"Code: {pairingCode}");
                _logger.LogInformation($"Expires in 10 minutes");
                _logger.LogInformation("===========================================");

                // Also log to console for visibility
                Console.WriteLine("\n===========================================");
                Console.WriteLine("VATSIM Companion - Pairing Code");
                Console.WriteLine("===========================================");
                Console.WriteLine($"  Code: {pairingCode}");
                Console.WriteLine($"  URL:  {bridgeUrl}");
                Console.WriteLine("===========================================\n");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting pairing");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Verify pairing code and issue token
        /// </summary>
        [HttpPost("verify")]
        public async Task<IActionResult> VerifyPairing([FromBody] VerifyPairingRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.PairingCode))
                {
                    return BadRequest(new { success = false, error = "Pairing code is required" });
                }

                if (string.IsNullOrEmpty(request.DeviceId))
                {
                    return BadRequest(new { success = false, error = "Device ID is required" });
                }

                // Validate pairing code
                if (!_pairingService.ValidatePairingCode(request.PairingCode))
                {
                    return BadRequest(new { success = false, error = "Invalid or expired pairing code" });
                }

                // Generate user ID (you can customize this logic)
                string userId = $"user_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

                // Generate JWT token
                string token = _pairingService.GenerateToken(userId, request.DeviceId);

                // Invalidate pairing code (one-time use)
                _pairingService.InvalidatePairingCode(request.PairingCode);

                // Register push notification token if provided
                if (!string.IsNullOrEmpty(request.FcmToken))
                {
                    await _pushService.RegisterTokenAsync(userId, request.FcmToken, request.DeviceId);
                }

                var response = new
                {
                    success = true,
                    token = token,
                    userId = userId,
                    expiresIn = int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "1440") * 60 // seconds
                };

                _logger.LogInformation($"Pairing successful for device {request.DeviceId}");
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}] Device paired: {request.DeviceName ?? request.DeviceId}");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying pairing");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Refresh JWT token
        /// </summary>
        [HttpPost("refresh")]
        public IActionResult RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                // Validate existing token
                var principal = _pairingService.ValidateToken(request.Token);

                if (principal == null)
                {
                    return Unauthorized(new { success = false, error = "Invalid token" });
                }

                var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var deviceId = principal.FindFirst("device_id")?.Value;

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(deviceId))
                {
                    return Unauthorized(new { success = false, error = "Invalid token claims" });
                }

                // Generate new token
                string newToken = _pairingService.GenerateToken(userId, deviceId);

                var response = new
                {
                    success = true,
                    token = newToken,
                    expiresIn = int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "1440") * 60
                };

                _logger.LogInformation($"Token refreshed for user {userId}");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        private string GenerateQRCode(string data)
        {
            try
            {
                using (var qrGenerator = new QRCodeGenerator())
                {
                    var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
                    var pngQrCode = new PngByteQRCode(qrCodeData);
                    byte[] imageBytes = pngQrCode.GetGraphic(20);
                    return $"data:image/png;base64,{Convert.ToBase64String(imageBytes)}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to generate QR code: {ex.Message}");
                return null;
            }
        }
    }

    public class VerifyPairingRequest
    {
        public string PairingCode { get; set; }
        public string DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public string? FcmToken { get; set; }
    }

    public class RefreshTokenRequest
    {
        public string Token { get; set; }
    }
}
