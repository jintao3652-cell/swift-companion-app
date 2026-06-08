@echo off
echo ============================================
echo Swift Companion App - Setup Script
echo ============================================
echo.

REM Check .NET
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: .NET SDK not found. Please install .NET 8.0 SDK first.
    echo Download from: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

where flutter >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo WARNING: Flutter not found. You'll need it for the mobile app.
    echo Download from: https://flutter.dev/docs/get-started/install
)

echo .NET SDK: OK
echo.

REM Setup Bridge Service
echo [1/2] Setting up Bridge Service...
cd bridge-service\windows\VatsimBridge
if not exist "VatsimBridge.sln" (
    cd ..
)

echo Restoring NuGet packages...
dotnet restore
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to restore Bridge packages
    pause
    exit /b 1
)

echo Building Bridge Service...
dotnet build --configuration Release
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to build Bridge Service
    pause
    exit /b 1
)

cd ..\..\..
echo Bridge Service: OK
echo.

REM Setup Flutter App
echo [2/2] Setting up Flutter App...
cd mobile-app

if exist "pubspec.yaml" (
    where flutter >nul 2>nul
    if %ERRORLEVEL% EQU 0 (
        echo Getting Flutter packages...
        flutter pub get
        if %ERRORLEVEL% EQU 0 (
            echo Flutter App: OK
        ) else (
            echo WARNING: Flutter pub get failed
        )
    ) else (
        echo SKIPPED: Flutter not installed
    )
) else (
    echo ERROR: pubspec.yaml not found
)

cd ..
echo.

REM Generate configuration template
echo Creating .env.example...
(
echo # Swift Companion Configuration
echo.
echo # Bridge Service
echo BRIDGE_PORT=5000
echo JWT_SECRET=CHANGE_THIS_TO_A_RANDOM_32_CHAR_STRING
echo.
echo # Firebase (Optional - for push notifications^)
echo FCM_SERVER_KEY=YOUR_FCM_SERVER_KEY_HERE
) > .env.example

echo.
echo ============================================
echo Setup Complete!
echo ============================================
echo.
echo NEXT STEPS:
echo.
echo 1. Install Cloudflare Tunnel (optional^):
echo    winget install --id Cloudflare.cloudflared
echo.
echo 2. Configure Bridge Service:
echo    - Edit bridge-service\windows\VatsimBridge\appsettings.json
echo    - Change JWT SecretKey to a random string
echo.
echo 3. Start services:
echo    start-bridge.bat
echo.
echo 4. Firebase Setup (for push notifications^):
echo    - Create Firebase project at console.firebase.google.com
echo    - Add Android app, download google-services.json
echo    - Place in mobile-app\android\app\
echo    - Add iOS app, download GoogleService-Info.plist
echo    - Place in mobile-app\ios\Runner\
echo.
pause
