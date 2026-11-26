# SmartTaskbar++ 2.0.0 — The Split Architecture Release 🚀

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
