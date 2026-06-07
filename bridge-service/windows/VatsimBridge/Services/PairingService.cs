using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace VatsimBridge.Services
{
    public interface IPairingService
    {
        string GeneratePairingCode();
        bool ValidatePairingCode(string code);
        string GenerateToken(string userId, string deviceId);
        ClaimsPrincipal ValidateToken(string token);
        void InvalidatePairingCode(string code);
    }

    /// <summary>
    /// Service for device pairing and JWT token management
    /// </summary>
    public class PairingService : IPairingService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<PairingService> _logger;
        private readonly Dictionary<string, PairingCodeInfo> _pairingCodes;
        private readonly object _lockObject = new object();

        public PairingService(IConfiguration configuration, ILogger<PairingService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _pairingCodes = new Dictionary<string, PairingCodeInfo>();
        }

        /// <summary>
        /// Generate a 6-digit pairing code
        /// </summary>
        public string GeneratePairingCode()
        {
            lock (_lockObject)
            {
                // Generate random 6-digit code
                var random = new Random();
                string code = random.Next(100000, 999999).ToString();

                // Store code with expiration
                var codeInfo = new PairingCodeInfo
                {
                    Code = code,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                };

                _pairingCodes[code] = codeInfo;

                _logger.LogInformation($"Generated pairing code: {code}");

                return code;
            }
        }

        /// <summary>
        /// Validate pairing code
        /// </summary>
        public bool ValidatePairingCode(string code)
        {
            lock (_lockObject)
            {
                if (string.IsNullOrEmpty(code))
                {
                    return false;
                }

                if (!_pairingCodes.TryGetValue(code, out var codeInfo))
                {
                    _logger.LogWarning($"Invalid pairing code: {code}");
                    return false;
                }

                if (DateTime.UtcNow > codeInfo.ExpiresAt)
                {
                    _logger.LogWarning($"Expired pairing code: {code}");
                    _pairingCodes.Remove(code);
                    return false;
                }

                _logger.LogInformation($"Valid pairing code: {code}");
                return true;
            }
        }

        /// <summary>
        /// Invalidate pairing code after successful pairing
        /// </summary>
        public void InvalidatePairingCode(string code)
        {
            lock (_lockObject)
            {
                if (_pairingCodes.Remove(code))
                {
                    _logger.LogInformation($"Invalidated pairing code: {code}");
                }
            }
        }

        /// <summary>
        /// Generate JWT token for authenticated device
        /// </summary>
        public string GenerateToken(string userId, string deviceId)
        {
            var secretKey = _configuration["Jwt:SecretKey"];
            var issuer = _configuration["Jwt:Issuer"];
            var audience = _configuration["Jwt:Audience"];
            var expirationMinutes = int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "1440");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim("device_id", deviceId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
                signingCredentials: credentials
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            _logger.LogInformation($"Generated token for user {userId}, device {deviceId}");

            return tokenString;
        }

        /// <summary>
        /// Validate JWT token
        /// </summary>
        public ClaimsPrincipal ValidateToken(string token)
        {
            var secretKey = _configuration["Jwt:SecretKey"];
            var issuer = _configuration["Jwt:Issuer"];
            var audience = _configuration["Jwt:Audience"];

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            try
            {
                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
                return principal;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Token validation failed: {ex.Message}");
                return null;
            }
        }

        private class PairingCodeInfo
        {
            public string Code { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
        }
    }
}
