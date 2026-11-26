@echo off
setlocal

echo ========================================================
echo Building SmartTaskbar++ Pure C Win32 Daemon
echo ========================================================
echo.

set OUTPUT_DIR=%~1
if "%OUTPUT_DIR%"=="" set OUTPUT_DIR=.

REM Check for MSVC cl.exe
where cl.exe >nul 2>&1
if %ERRORLEVEL% EQU 0 goto BUILD_MSVC

REM Check for GCC
where gcc.exe >nul 2>&1
if %ERRORLEVEL% EQU 0 goto BUILD_GCC_SYSTEM

if exist "C:\Users\anmol\tools\w64devkit\bin\gcc.exe" goto BUILD_GCC_W64DEVKIT

echo [ERROR] Neither MSVC (cl.exe) nor GCC (gcc.exe) was found.
exit /b 1

:BUILD_MSVC
echo [INFO] Detected MSVC compiler (cl.exe). Compiling with MSVC...
rc.exe /nologo /fo daemon.res daemon.rc
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Resource compilation failed.
    exit /b 1
)
cl.exe /nologo /O2 /W3 /D_CRT_SECURE_NO_WARNINGS /Fe:"%OUTPUT_DIR%\SmartTaskbarDaemon.exe" main.c settings.c magnification_effect.c taskbar_engine.c tray.c daemon.res user32.lib shell32.lib gdi32.lib advapi32.lib dwmapi.lib /link /SUBSYSTEM:WINDOWS
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] MSVC build failed.
    exit /b 1
)
del /f /q daemon.res *.obj >nul 2>&1
echo [SUCCESS] SmartTaskbarDaemon.exe built successfully with MSVC!
exit /b 0

:BUILD_GCC_SYSTEM
set GCC_CMD=gcc.exe
set WINDRES_CMD=windres.exe
goto DO_GCC_BUILD

:BUILD_GCC_W64DEVKIT
set GCC_CMD=C:\Users\anmol\tools\w64devkit\bin\gcc.exe
set WINDRES_CMD=C:\Users\anmol\tools\w64devkit\bin\windres.exe
goto DO_GCC_BUILD

:DO_GCC_BUILD
echo [INFO] Compiling resources with %WINDRES_CMD%...
"%WINDRES_CMD%" -i daemon.rc -o daemon.res.o
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Resource compilation failed.
    exit /b 1
)

echo [INFO] Compiling daemon with %GCC_CMD%...
"%GCC_CMD%" -O2 -s -Wall -Wextra -std=c99 main.c settings.c magnification_effect.c taskbar_engine.c tray.c daemon.res.o -o "%OUTPUT_DIR%\SmartTaskbarDaemon.exe" -mwindows -lshell32 -luser32 -lgdi32 -ladvapi32 -ldwmapi
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] GCC compilation failed.
    exit /b 1
)

del /f /q daemon.res.o >nul 2>&1
echo.
echo [SUCCESS] SmartTaskbarDaemon.exe built successfully with GCC!
exit /b 0
