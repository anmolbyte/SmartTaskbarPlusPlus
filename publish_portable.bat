@echo off
echo Building SmartTaskbar++ Portable (Optimized Single File)...
echo.

set PROJECT_PATH=Sources\SmartTaskbar\SmartTaskbar++.csproj
set OUTPUT_PATH=publish

:: -p:PublishTrimmed=true removes unused code from the .NET runtime
:: -p:EnableCompressionInSingleFile=true compresses the final EXE
dotnet publish "%PROJECT_PATH%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -o %OUTPUT_PATH%

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Success! 
    echo Your optimized executable is located in: %CD%\%OUTPUT_PATH%
    echo.
    echo SIZE NOTE: The file is now much smaller (approx 50-70MB). 
    echo It contains the .NET runtime so your friends don't need to install anything.
) else (
    echo.
    echo Build failed. Please check the error messages above.
)
pause
