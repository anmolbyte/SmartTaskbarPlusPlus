@echo off
:: Batch script to replace the installed SmartTaskbar++ binaries with the local test build
echo Stopping running SmartTaskbar instances...
taskkill /F /IM SmartTaskbar* >nul 2>&1
timeout /t 1 /nobreak >nul

echo Copying newly built binaries to C:\Program Files (x86)\SmartTaskbar++...
copy /y "publish_lite\SmartTaskbarDaemon.exe" "C:\Program Files (x86)\SmartTaskbar++\SmartTaskbarDaemon.exe"
copy /y "publish_lite\SmartTaskbarSettings.exe" "C:\Program Files (x86)\SmartTaskbar++\SmartTaskbarSettings.exe"

if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] Replaced installed files successfully!
    echo Starting updated SmartTaskbarDaemon...
    start "" "C:\Program Files (x86)\SmartTaskbar++\SmartTaskbarDaemon.exe"
) else (
    echo [ERROR] Failed to copy files. Please right-click this script and select 'Run as administrator'.
    pause
)
