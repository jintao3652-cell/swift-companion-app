@echo off
echo ============================================
echo Swift Companion - APK Builder
echo ============================================
echo.

cd mobile-app

echo Cleaning previous builds...
flutter clean >nul 2>&1

echo Getting dependencies...
flutter pub get

echo.
echo Building APK (Release)...
echo This may take 5-10 minutes...
echo.

flutter build apk --release

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ============================================
    echo Build Success!
    echo ============================================
    echo.
    echo APK Location:
    echo %CD%\build\app\outputs\flutter-apk\app-release.apk
    echo.
    echo File size:
    for %%A in (build\app\outputs\flutter-apk\app-release.apk) do echo %%~zA bytes
    echo.
    echo Next: Transfer APK to your phone and install
    echo.
) else (
    echo.
    echo ============================================
    echo Build Failed!
    echo ============================================
    echo.
    echo Check the error messages above
    echo.
)

pause
