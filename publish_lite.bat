@echo off
echo Building SmartTaskbar++ Lite (Tiny 1MB File)...
echo.
echo NOTE: This version requires the .NET 6 Desktop Runtime to be installed on the target PC.
echo.

set DOTNET_CMD=dotnet
if exist "%LOCALAPPDATA%\dotnet\dotnet.exe" set DOTNET_CMD="%LOCALAPPDATA%\dotnet\dotnet.exe"

set PROJECT_PATH=Sources\SmartTaskbar\SmartTaskbar++.csproj
set OUTPUT_PATH=publish_lite

%DOTNET_CMD% publish "%PROJECT_PATH%" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o %OUTPUT_PATH%

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Success! 
    echo Your tiny executable is located in: %CD%\%OUTPUT_PATH%
    echo Size should be approx 1-2MB.
) else (
    echo.
    echo Build failed. Please check the error messages above.
)
pause
