# SmartTaskbar++ 2.1.0 — Native Brightness Control & Hotkeys 🎛️

SmartTaskbar++ 2.1 adds a fully native **hardware brightness control engine**, global keyboard shortcuts, and a redesigned unified Win32 right-click menu — all in pure C, zero dependencies.

---

## 🌟 What's New in 2.1.0

### 1. DDC/CI Hardware Brightness Control
* Controls **external monitor brightness over DDC/CI** (VCP code 0x10) using the Windows `Dxva2` API — no OSD required.
* Falls back to **WMI (`WmiMonitorBrightnessMethods`)** for internal laptop panels.
* Handles edge-case monitors that return a zero physical handle (fixed via `has_physical_handle` guard).
* Physical monitor handles are **re-acquired on every write** to survive sleep/wake and hotplug events.
* Brightness state is persisted in `AppSettings` and survives daemon restarts.

### 2. Global Hotkeys for Brightness
* Define any number of brightness shortcuts in `settings.json` via the `BrightnessShortcuts` JSON array.
* Supports full accelerator-string parsing: `Ctrl`, `Alt`, `Shift`, `Win` + any key.
* Default bindings: **Ctrl+Alt+Down** (dim) / **Ctrl+Alt+Up** (raise) in 10% increments.
* Registered via `RegisterHotKey` on daemon startup; unregistered cleanly on exit.
* Hotkey status displayed live in the Settings → Brightness tab.

### 3. Unified Native Win32 Right-Click Menu
* The entire tray right-click experience is now a **single custom Win32 popup window** — no HMENU, no system-drawn menu.
* **Live brightness slider** appears at the top of the menu on first click (no submenu needed).
* Screen Effects, Auto Mode, Hide when Fullscreen, Settings, and Exit — all in one dark/light-themed panel.
* Windows 11 **rounded corners** and DPI-aware sizing.
* Screen Effects opens as a native **cascading submenu flyout**.

### 4. Desktop & Process Reliability Fixes
* Daemon explicitly opens `WinSta0\Default` at startup (`OpenDesktopW` + `SetThreadDesktop`) — tray icon registers correctly even when launched by Task Scheduler or non-interactive callers.
* Double-clicking the desktop shortcut when the daemon is already running now **opens the Settings window** instead of silently exiting.
* Settings process is launched with `lpDesktop = "WinSta0\\Default"` — always visible on the user's desktop.
* `daemon.log` written to `%APPDATA%\SmartTaskbar\daemon.log` for diagnostics.

---

## 🌟 Also in this release (from v2.0.0)

SmartTaskbar++ 2.0 is a complete architectural transformation designed for **ultimate lightweightedness, native performance, and 4K display fidelity**.

---

## 🌟 Major Highlights & New Features

### 1. Split Daemon Architecture (Pure C Background Daemon + On-Demand C# UI)
* **Background Daemon (`SmartTaskbarDaemon.exe`)**:
  * Rewritten in **pure C Win32**.
  * Sits in your system tray at an astonishing **~1.5 MB of RAM** (down from ~35 MB) and **0.0% CPU**.
  * Instant cold start in **< 35 milliseconds** on Windows boot.
  * Handles the 125ms taskbar auto-hide loop, screen color effects, and quick-toggle icon clicks natively without loading the .NET CLR.
* **On-Demand Settings Companion (`SmartTaskbarSettings.exe`)**:
  * The rich C# WinForms settings window runs **ONLY** when you click *"Settings..."* from the tray menu.
  * When closed, the process **completely terminates and unloads 100% of its memory from RAM**, returning your system to the ~1.5 MB daemon.
  * Single-instance enforcement: clicking *"Settings..."* while already open restores and brings the existing window to the front.

### 2. High-DPI & 4K Fix (Crisp Native Pixels, Zero Blur)
* Integrated embedded application manifests with **`PerMonitorV2` High-DPI Awareness**.
* Windows will **never bitmap-stretch** the window. Text, borders, and controls now render directly to native hardware pixels on 4K, 1440p, and mixed-DPI multi-monitor setups.

### 3. Native Win32 System Tray Menu
* The right-click tray menu is now a 100% native Win32 popup menu (`TrackPopupMenuEx`).
* Transient memory: menu resources are created on demand and destroyed (`DestroyMenu`) the microsecond you click away.
* Full menu access to Auto Mode toggles, Fullscreen auto-hide overrides, Screen Effects (Negative, Smart Inversion variations, Night Red, Grayscale, Sepia), and Settings.

### 4. Smart Display Detection (Clamshell / Lid Closed Support)
* The daemon now cross-references internal laptop panels with `EnumDisplayDevices` (`DISPLAY_DEVICE_ATTACHED_TO_DESKTOP`).
* When your laptop lid is closed (or when using *"Second screen only"*), the inactive internal screen is automatically hidden from menus and brightness controls.
* Opening the lid instantly restores the panel via `WM_DISPLAYCHANGE`.

### 5. Multi-Threaded Concurrency & Race Condition Hardening
* **Dual-Channel Named Event Synchronization**: Dedicated channels (`SmartTaskbar_ReloadSettings` and `SmartTaskbar_UIReloadSettings`) prevent AutoReset event listener competition between the daemon and UI.
* **Atomic JSON Storage**: Settings are written to `.tmp` files and swapped via atomic filesystem replacements (`MoveFileExW` / `File.Move`), eliminating partially read configurations.
* Stress-tested with 8 concurrent OS threads performing 400 simultaneous operations with 0 errors or lock collisions.

---

## 📦 What's Included

* **`SmartTaskbar_v2.0.0_Setup.exe`**: Inno Setup installer that registers the C daemon for Windows startup.
* **`SmartTaskbarDaemon.exe`**: The standalone pure C background daemon (~900 KB).
* **`SmartTaskbarSettings.exe`**: The standalone on-demand C# settings companion.
