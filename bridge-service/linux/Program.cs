using SwiftBridge.Hubs;
using SwiftBridge.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Validate critical configuration at startup
var jwtSecret = builder.Configuration["Jwt:SecretKey"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

if (string.IsNullOrEmpty(jwtSecret) || jwtSecret.Length < 32)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("ERROR: Jwt:SecretKey must be at least 32 characters long!");
    Console.WriteLine("Update appsettings.json before starting the bridge.");
    Console.ResetColor();
    Environment.Exit(1);
}

if (string.IsNullOrEmpty(jwtIssuer) || string.IsNullOrEmpty(jwtAudience))
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("WARNING: Jwt:Issuer or Jwt:Audience not configured!");
    Console.ResetColor();
}

builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "[yyyy-MM-ddTHH:mm:ss.fffZ] ";
    options.UseUtcTimestamp = true;
    options.SingleLine = true;
});

var bridgePort = builder.Configuration["Port"] ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{bridgePort}");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

builder.Services.AddHttpClient<IPushNotificationService, PushNotificationService>();

builder.Services.AddSingleton<IPushNotificationService, PushNotificationService>();
builder.Services.AddSingleton<IPairingService, PairingService>();
builder.Services.AddSingleton<IMessageStorageService, MessageStorageService>();
builder.Services.AddSingleton<SwiftDbusService>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

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
    name = "Swift Companion Bridge",
    version = "1.0.0",
    status = "running",
    platform = "Linux",
    endpoints = new
    {
        signalr = "/swifthub",
        api = "/api",
        swagger = "/swagger"
    }
}));

Console.WriteLine("===========================================");
Console.WriteLine("Swift Companion Bridge");
Console.WriteLine("===========================================");
Console.WriteLine($"Bridge URL: http://localhost:{builder.Configuration["Port"] ?? "5000"}");
Console.WriteLine("SignalR Hub: /swifthub");
Console.WriteLine("API Docs: /swagger");
Console.WriteLine("===========================================");
Console.WriteLine("");
Console.WriteLine("To start pairing:");
Console.WriteLine("  1. Start swift and connect to VATSIM");
Console.WriteLine("  2. Open mobile app");
Console.WriteLine("  3. Generate pairing code: POST /api/pairing/start");
Console.WriteLine("  4. Enter code in mobile app");
Console.WriteLine("===========================================");

var swiftService = app.Services.GetRequiredService<SwiftDbusService>();
_ = Task.Run(async () =>
{
    await Task.Delay(2000);
    await swiftService.ConnectAsync();
});

app.Run();
