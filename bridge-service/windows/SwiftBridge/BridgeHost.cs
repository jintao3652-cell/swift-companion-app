using SwiftBridge.Hubs;
using SwiftBridge.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace SwiftBridge;

/// <summary>
/// Builds the configured Bridge <see cref="WebApplication"/>.
/// Hosted either standalone (console, via Program.cs) or in-process (the WPF desktop app).
/// </summary>
public static class BridgeHost
{
    /// <summary>
    /// Builds and configures the Bridge web app without starting it.
    /// </summary>
    public static WebApplication? BuildApp(int? port = null, string? publicUrl = null, string[]? args = null)
    {
        var builder = WebApplication.CreateBuilder(args ?? Array.Empty<string>());

        // 统一日志格式
        builder.Logging.AddSimpleConsole(options =>
        {
            options.TimestampFormat = "[yyyy-MM-ddTHH:mm:ss.fffZ] ";
            options.UseUtcTimestamp = true;
            options.SingleLine = true;
        });

        // Allow callers (WPF) to override port via in-memory config.
        var overrides = new Dictionary<string, string?>();
        if (port.HasValue) overrides["Port"] = port.Value.ToString();
        if (!string.IsNullOrEmpty(publicUrl)) overrides["PublicUrl"] = publicUrl;
        if (overrides.Count > 0) builder.Configuration.AddInMemoryCollection(overrides);

        // Listen on all network interfaces so phones on the same LAN can connect
        var bridgePort = builder.Configuration["Port"] ?? "5000";
        builder.WebHost.UseUrls($"http://0.0.0.0:{bridgePort}");

        builder.Services.AddControllers()
            // In-process hosting (WPF): the entry assembly is the desktop exe, so MVC must
            // scan this assembly explicitly or every /api/* route 404s.
            .AddApplicationPart(typeof(BridgeHost).Assembly);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // SignalR
        builder.Services.AddSignalR(options =>
        {
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(120);
            options.KeepAliveInterval = TimeSpan.FromSeconds(30);
        })
        .AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.PropertyNamingPolicy =
                System.Text.Json.JsonNamingPolicy.CamelCase;
        });

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        // JWT Authentication
        var jwtSecretKey = builder.Configuration["Jwt:SecretKey"];
        if (!string.IsNullOrEmpty(jwtSecretKey))
        {
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
                        ValidateIssuer = true,
                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidateAudience = true,
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/swifthub"))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                });
        }

        // HTTP Clients（推送通知用）
        builder.Services.AddHttpClient<IPushNotificationService, PushNotificationService>();
        builder.Services.AddSingleton<IPushNotificationService, PushNotificationService>();

        // Services
        builder.Services.AddSingleton<IPairingService, PairingService>();
        builder.Services.AddSingleton<IMessageStorageService, MessageStorageService>();

        // swift 数据服务：同一实例既作单例供查询，又作后台服务连接 swiftCore DBus
        builder.Services.AddSingleton<SwiftDataService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<SwiftDataService>());

        var app = builder.Build();

        app.UseSwagger();
        app.UseSwaggerUI();
        app.UseCors("AllowAll");
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<SwiftHub>("/swifthub");

        app.MapGet("/", () => Results.Json(new
        {
            name = "swift Companion Bridge",
            version = "2.0.0",
            status = "running",
            endpoints = new
            {
                signalr = "/swifthub",
                api = "/api",
                swagger = "/swagger"
            }
        }));

        return app;
    }
}
