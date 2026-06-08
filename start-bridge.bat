@echo off
setlocal enabledelayedexpansion

echo ============================================
echo Swift Companion - Bridge + Cloudflare Tunnel Launcher
echo ============================================
echo.

REM Check .NET
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: .NET SDK not found
    pause
    exit /b 1
)

REM Check cloudflared
where cloudflared >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo WARNING: cloudflared not found. Installing...
    echo Download from: https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/
    echo.
    echo Or install via winget:
    echo   winget install --id Cloudflare.cloudflared
    echo.
    pause
    exit /b 1
)

REM Start Bridge Service
echo [1/2] Starting Bridge Service...
cd bridge-service\windows\VatsimBridge
start "Swift Bridge" dotnet run --configuration Release
timeout /t 3 /nobreak >nul
echo Bridge running on http://localhost:5000
echo.

REM Start Cloudflare Tunnel
echo [2/2] Starting Cloudflare Tunnel...
cd ..\..\..

REM Check for existing tunnel config
if exist "%USERPROFILE%\.cloudflared\config.yml" (
    echo Using existing tunnel configuration
    start "Cloudflare Tunnel" cloudflared tunnel run
) else (
    echo No tunnel configured. Starting quick tunnel...
    echo NOTE: Quick tunnels give random URLs each time
    echo For permanent URL, run: cloudflared tunnel login
    echo.
    start "Cloudflare Tunnel" cloudflared tunnel --url http://localhost:5000
)

echo.
echo ============================================
echo Services Started!
echo ============================================
echo.
echo Bridge Service: http://localhost:5000
echo Cloudflare Tunnel: Check the terminal window for public URL
echo.
echo API Endpoints:
echo   GET  /api/status
echo   GET  /api/swift/state
echo   POST /api/notifications/send
echo.
echo To stop: Close the terminal windows
echo.
pause
