@echo off
setlocal

echo ========================================================
echo Building SmartTaskbar++ Split Architecture
echo   1. SmartTaskbarDaemon (Pure C Win32 Background Daemon)
echo   2. SmartTaskbarSettings (C# WinForms Settings UI)
echo ========================================================
echo.

set OUTPUT_PATH=%~1
if "%OUTPUT_PATH%"=="" set OUTPUT_PATH=publish_lite

if not exist "%OUTPUT_PATH%" mkdir "%OUTPUT_PATH%"

REM --- 1. Build C Win32 Daemon ---
echo [STEP 1/2] Building Pure C Win32 Daemon...
pushd Sources\SmartTaskbarDaemon
call build_daemon.bat "..\..\%OUTPUT_PATH%"
set DAEMON_RESULT=%ERRORLEVEL%
popd

if %DAEMON_RESULT% NEQ 0 (
    echo [ERROR] Daemon build failed with error code %DAEMON_RESULT%.
    exit /b %DAEMON_RESULT%
)

REM --- 2. Build C# Settings UI ---
echo.
echo [STEP 2/2] Building C# WinForms Settings UI...
set DOTNET_CMD=dotnet
if exist "%LOCALAPPDATA%\dotnet\dotnet.exe" set DOTNET_CMD="%LOCALAPPDATA%\dotnet\dotnet.exe"

set PROJECT_PATH=Sources\SmartTaskbar\SmartTaskbar++.csproj

%DOTNET_CMD% publish "%PROJECT_PATH%" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "%OUTPUT_PATH%"
set SETTINGS_RESULT=%ERRORLEVEL%

if %SETTINGS_RESULT% NEQ 0 (
    echo [ERROR] Settings UI build failed with error code %SETTINGS_RESULT%.
    exit /b %SETTINGS_RESULT%
)

echo.
echo ========================================================
echo [SUCCESS] Split Daemon Architecture Built Successfully!
echo Output directory: %CD%\%OUTPUT_PATH%
echo   - %OUTPUT_PATH%\SmartTaskbarDaemon.exe
echo   - %OUTPUT_PATH%\SmartTaskbarSettings.exe
echo ========================================================
exit /b 0
