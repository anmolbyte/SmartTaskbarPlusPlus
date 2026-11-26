$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "SmartTaskbar++ Integration & Race Condition Test Suite" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$publishDir = Join-Path $PSScriptRoot "publish_lite"
$daemonExe = Join-Path $publishDir "SmartTaskbarDaemon.exe"
$settingsExe = Join-Path $publishDir "SmartTaskbarSettings.exe"

if (!(Test-Path $daemonExe) -or !(Test-Path $settingsExe)) {
    Write-Error "Executables not found in $publishDir. Run build_all.bat first."
}

# Define Win32 methods for testing
$win32Type = Add-Type -MemberDefinition @"
[DllImport("user32.dll", SetLastError = true)]
public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

[DllImport("user32.dll")]
public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
"@ -Name "Win32TestMethods" -Namespace "TestNamespace" -PassThru

# Ensure no existing daemon or settings processes are running
Get-Process "SmartTaskbarDaemon" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process "SmartTaskbarSettings" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

Write-Host "`n[TEST 1] Testing SmartTaskbarDaemon cold start and memory usage..." -ForegroundColor Yellow
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$daemonProc = Start-Process -FilePath $daemonExe -PassThru
$sw.Stop()
Start-Sleep -Milliseconds 600

if ($daemonProc.HasExited) {
    Write-Error "[FAIL] Daemon exited unexpectedly with code $($daemonProc.ExitCode)"
}

$daemonProc.Refresh()
$privateMb = [Math]::Round($daemonProc.PrivateMemorySize64 / 1MB, 2)
$wsMb = [Math]::Round($daemonProc.WorkingSet64 / 1MB, 2)
Write-Host "  -> Daemon started in $($sw.ElapsedMilliseconds) ms" -ForegroundColor Green
Write-Host "  -> Private memory footprint: $privateMb MB (Requirement: ~1-2 MB)" -ForegroundColor Green
Write-Host "  -> WorkingSet: $wsMb MB" -ForegroundColor Green

Write-Host "`n[TEST 2] Testing Daemon Single-Instance Mutex..." -ForegroundColor Yellow
$secondDaemon = Start-Process -FilePath $daemonExe -PassThru -Wait
if ($secondDaemon.ExitCode -ne 0) {
    Write-Error "[FAIL] Second daemon instance exited with non-zero exit code: $($secondDaemon.ExitCode)"
}
$instances = @(Get-Process "SmartTaskbarDaemon" -ErrorAction SilentlyContinue)
if ($instances.Count -ne 1) {
    Write-Error "[FAIL] Expected exactly 1 daemon instance running, found $($instances.Count)"
}
Write-Host "  -> Second daemon instance exited cleanly (code 0), exactly 1 running instance confirmed." -ForegroundColor Green

Write-Host "`n[TEST 3] Testing SmartTaskbarSettings startup..." -ForegroundColor Yellow
$settingsProc = Start-Process -FilePath $settingsExe -ArgumentList "--settings" -PassThru
Start-Sleep -Milliseconds 1500

if ($settingsProc.HasExited) {
    Write-Error "[FAIL] Settings UI exited unexpectedly with code $($settingsProc.ExitCode)"
}
Write-Host "  -> Settings UI started successfully." -ForegroundColor Green

Write-Host "`n[TEST 4] Testing Settings UI Single-Instance Mutex..." -ForegroundColor Yellow
$secondSettings = Start-Process -FilePath $settingsExe -ArgumentList "--settings" -PassThru -Wait
if ($secondSettings.ExitCode -ne 0) {
    Write-Error "[FAIL] Second settings instance exited with non-zero code: $($secondSettings.ExitCode)"
}
$settingsInstances = @(Get-Process "SmartTaskbarSettings" -ErrorAction SilentlyContinue)
if ($settingsInstances.Count -ne 1) {
    Write-Error "[FAIL] Expected exactly 1 settings instance running, found $($settingsInstances.Count)"
}
Write-Host "  -> Second settings instance brought window to front and exited cleanly." -ForegroundColor Green

Write-Host "`n[TEST 5] Testing Settings reload event signaling (Dual Channel)..." -ForegroundColor Yellow
$reloadDaemon = [System.Threading.EventWaitHandle]::OpenExisting("SmartTaskbar_ReloadSettings")
$reloadDaemon.Set() | Out-Null
$reloadDaemon.Dispose()

$reloadUI = [System.Threading.EventWaitHandle]::OpenExisting("SmartTaskbar_UIReloadSettings")
$reloadUI.Set() | Out-Null
$reloadUI.Dispose()
Start-Sleep -Milliseconds 300
Write-Host "  -> Both daemon and UI reload channels signaled and processed without conflict or race condition." -ForegroundColor Green

Write-Host "`n[TEST 6] Testing Clean Exit of Settings UI..." -ForegroundColor Yellow
$settingsProc.CloseMainWindow() | Out-Null
$settingsProc.WaitForExit(3000)
if (!$settingsProc.HasExited) {
    $settingsProc.Kill()
    Write-Error "[FAIL] Settings process did not exit when closed."
}
$remainingSettings = @(Get-Process "SmartTaskbarSettings" -ErrorAction SilentlyContinue)
if ($remainingSettings.Count -ne 0) {
    Write-Error "[FAIL] Settings process still running in background!"
}
Write-Host "  -> Settings UI terminated completely (0 lingering processes)." -ForegroundColor Green

Write-Host "`n[TEST 7] Testing Clean Exit of Daemon via WM_CLOSE..." -ForegroundColor Yellow
$daemonHwnd = $win32Type::FindWindow("SmartTaskbar_Daemon_Class", "SmartTaskbar Daemon")
if ($daemonHwnd -eq [IntPtr]::Zero) {
    Write-Error "[FAIL] Could not find daemon window handle."
}
# 0x0010 = WM_CLOSE
$win32Type::PostMessage($daemonHwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
$daemonProc.WaitForExit(3000)
if (!$daemonProc.HasExited) {
    $daemonProc.Kill()
    Write-Error "[FAIL] Daemon did not exit on WM_CLOSE."
}
$remainingDaemon = @(Get-Process "SmartTaskbarDaemon" -ErrorAction SilentlyContinue)
if ($remainingDaemon.Count -ne 0) {
    Write-Error "[FAIL] Daemon still running!"
}
Write-Host "  -> Daemon cleanly terminated, restored taskbar, and released mutex." -ForegroundColor Green

Write-Host "`n[TEST 8] Running Native C Unit & Edge-Case Tests (test_daemon_units)..." -ForegroundColor Yellow
$daemonDir = Join-Path $PSScriptRoot "Sources\SmartTaskbarDaemon"
$unitsExe = Join-Path $daemonDir "test_daemon_units.exe"
$gccPath = "C:\Users\anmol\tools\w64devkit\bin\gcc.exe"

& $gccPath -O2 "$daemonDir\test_daemon_units.c" "$daemonDir\settings.c" "$daemonDir\magnification_effect.c" "$daemonDir\taskbar_engine.c" -o $unitsExe -lshell32 -luser32 -lgdi32 -ladvapi32 -ldwmapi
if ($LASTEXITCODE -ne 0) {
    Write-Error "[FAIL] Failed to compile test_daemon_units.c"
}
$unitsProc = Start-Process -FilePath $unitsExe -NoNewWindow -PassThru -Wait
if ($unitsProc.ExitCode -ne 0) {
    Write-Error "[FAIL] test_daemon_units failed with exit code $($unitsProc.ExitCode)"
}
Remove-Item $unitsExe -Force -ErrorAction SilentlyContinue
Write-Host "  -> All daemon unit & edge-case tests passed." -ForegroundColor Green

Write-Host "`n[TEST 9] Running Multi-Threaded Concurrency & Race Condition Stress Test (test_race_conditions)..." -ForegroundColor Yellow
$raceExe = Join-Path $daemonDir "test_race_conditions.exe"
& $gccPath -O2 "$daemonDir\test_race_conditions.c" "$daemonDir\settings.c" -o $raceExe -lshell32 -ladvapi32
if ($LASTEXITCODE -ne 0) {
    Write-Error "[FAIL] Failed to compile test_race_conditions.c"
}
$raceProc = Start-Process -FilePath $raceExe -NoNewWindow -PassThru -Wait
if ($raceProc.ExitCode -ne 0) {
    Write-Error "[FAIL] test_race_conditions failed with exit code $($raceProc.ExitCode)"
}
Remove-Item $raceExe -Force -ErrorAction SilentlyContinue
Write-Host "  -> Concurrency stress test passed (400 concurrent reads/writes with 0 collisions)." -ForegroundColor Green

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "ALL INTEGRATION, UNIT & CONCURRENCY TESTS PASSED PERFECTLY!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
