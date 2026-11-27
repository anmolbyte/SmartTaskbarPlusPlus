#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shellapi.h>
#include <stdbool.h>
#include <stdio.h>
#include "settings.h"
#include "magnification_effect.h"
#include "taskbar_engine.h"
#include "tray.h"
#include "resource.h"
#include "brightness.h"
#include "flyout.h"
#include "hotkeys.h"

#define IDT_ENGINE_TIMER 1

void daemon_log(const char *fmt, ...) {
    wchar_t path[MAX_PATH];
    if (!settings_get_path(path, MAX_PATH)) return;
    wchar_t *slash = wcsrchr(path, L'\\');
    if (!slash) return;
    *slash = L'\0';
    wchar_t log_path[MAX_PATH];
    swprintf(log_path, MAX_PATH, L"%ls\\daemon.log", path);

    FILE *f = _wfopen(log_path, L"a");
    if (!f) return;

    SYSTEMTIME st;
    GetLocalTime(&st);
    fprintf(f, "[%02d:%02d:%02d.%03d] ", st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);

    va_list args;
    va_start(args, fmt);
    vfprintf(f, fmt, args);
    va_end(args);
    fprintf(f, "\n");
    fclose(f);
}

static AppSettings g_settings;
static HANDLE g_hReloadEvent = NULL;
static HANDLE g_hWaitHandle = NULL;
static HANDLE g_hDaemonMutex = NULL;

static VOID CALLBACK settings_reload_callback(PVOID lpParam, BOOLEAN TimerOrWaitFired) {
    (void)TimerOrWaitFired;
    HWND hwnd = (HWND)lpParam;
    if (hwnd && IsWindow(hwnd)) {
        PostMessageW(hwnd, WM_APP_RELOAD_SETTINGS, 0, 0);
    }
}

static LRESULT CALLBACK WndProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam) {
    UINT restartMsg = tray_get_taskbar_restart_msg();
    if (restartMsg != 0 && uMsg == restartMsg) {
        tray_on_message(hwnd, uMsg, wParam, lParam, &g_settings);
        return 0;
    }

    switch (uMsg) {
        case WM_CREATE:
            SetTimer(hwnd, IDT_ENGINE_TIMER, 125, NULL);
            return 0;

        case WM_TIMER:
            if (wParam == IDT_ENGINE_TIMER) {
                taskbar_engine_tick(&g_settings);
            }
            return 0;

        case WM_APP_TRAYMSG:
            tray_on_message(hwnd, uMsg, wParam, lParam, &g_settings);
            return 0;

        case WM_HOTKEY:
            daemon_log("WndProc: WM_HOTKEY id=0x%Ix lParam=0x%Ix", (size_t)wParam, (size_t)lParam);
            hotkeys_handle_message(wParam, lParam);
            return 0;

        case WM_APP_SHOW_SETTINGS:
            daemon_log("WndProc: WM_APP_SHOW_SETTINGS");
            tray_launch_settings(hwnd);
            return 0;

        case WM_APP_SHOW_FLYOUT:
            daemon_log("WndProc: WM_APP_SHOW_FLYOUT");
            flyout_toggle(hwnd);
            return 0;

        case WM_APP_RELOAD_SETTINGS:
            settings_load(&g_settings);
            if (g_settings.is_negative_enabled) {
                mag_apply_effect(g_settings.active_color_effect);
            } else {
                mag_restore_default();
            }
            if (g_settings.auto_mode == AUTO_MODE_NONE) {
                taskbar_engine_restore(true);
            }
            taskbar_engine_invalidate_cache();
            brightness_refresh_monitors();
            hotkeys_reload();
            return 0;

        case WM_SETTINGCHANGE:
            tray_update_icon();
            return 0;

        case WM_DISPLAYCHANGE:
            taskbar_engine_invalidate_cache();
            brightness_refresh_monitors();
            hotkeys_reload();
            return 0;

        case WM_QUERYENDSESSION:
            return TRUE;

        case WM_ENDSESSION:
            if (wParam) {
                KillTimer(hwnd, IDT_ENGINE_TIMER);
                hotkeys_cleanup();
                flyout_cleanup();
                brightness_cleanup();
                taskbar_engine_stop(&g_settings);
                mag_uninit();
                tray_cleanup();
            }
            return 0;

        case WM_CLOSE:
            daemon_log("WndProc: WM_CLOSE received, destroying window");
            DestroyWindow(hwnd);
            return 0;

        case WM_DESTROY:
            daemon_log("WndProc: WM_DESTROY received");
            KillTimer(hwnd, IDT_ENGINE_TIMER);
            if (g_hWaitHandle) {
                UnregisterWaitEx(g_hWaitHandle, INVALID_HANDLE_VALUE);
                g_hWaitHandle = NULL;
            }
            if (g_hReloadEvent) {
                CloseHandle(g_hReloadEvent);
                g_hReloadEvent = NULL;
            }
            hotkeys_cleanup();
            flyout_cleanup();
            brightness_cleanup();
            taskbar_engine_stop(&g_settings);
            mag_uninit();
            tray_cleanup();
            daemon_log("WndProc: PostQuitMessage(0)");
            PostQuitMessage(0);
            return 0;
    }

    return DefWindowProcW(hwnd, uMsg, wParam, lParam);
}

static LONG WINAPI unhandled_exception_handler(EXCEPTION_POINTERS *pExceptionInfo) {
    daemon_log("CRASH: Unhandled Exception Code 0x%08X at address %p",
               pExceptionInfo->ExceptionRecord->ExceptionCode,
               pExceptionInfo->ExceptionRecord->ExceptionAddress);
    return EXCEPTION_CONTINUE_SEARCH;
}

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPWSTR lpCmdLine, int nShowCmd) {
    (void)hPrevInstance; (void)lpCmdLine; (void)nShowCmd;

    // Attach thread to user's interactive desktop
    HDESK hDesk = OpenDesktopW(L"Default", 0, FALSE, GENERIC_ALL);
    if (hDesk) {
        SetThreadDesktop(hDesk);
    }

    SetUnhandledExceptionFilter(unhandled_exception_handler);

    daemon_log("wWinMain: starting SmartTaskbarDaemon");

    // 1. Single Instance Check
    g_hDaemonMutex = CreateMutexW(NULL, TRUE, DAEMON_MUTEX_NAME);
    if (!g_hDaemonMutex || GetLastError() == ERROR_ALREADY_EXISTS) {
        daemon_log("wWinMain: another instance already exists (error %lu)", GetLastError());
        if (g_hDaemonMutex) CloseHandle(g_hDaemonMutex);

        bool is_silent = (lpCmdLine && (wcsstr(lpCmdLine, L"--startup") || wcsstr(lpCmdLine, L"--silent") || wcsstr(lpCmdLine, L"--background")));
        if (!is_silent) {
            HWND hExisting = FindWindowW(L"SmartTaskbar_Daemon_Class", L"SmartTaskbar Daemon");
            daemon_log("wWinMain: re-launch signaling existing daemon %p to open settings", (void*)hExisting);
            if (hExisting) {
                AllowSetForegroundWindow(ASFW_ANY);
                PostMessageW(hExisting, WM_APP_SHOW_SETTINGS, 0, 0);
            }
        }
        return 0; // Daemon is already running
    }
    daemon_log("wWinMain: acquired daemon mutex");

    // 2. High DPI Awareness
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    // 3. Register Hidden Window Class
    const wchar_t CLASS_NAME[] = L"SmartTaskbar_Daemon_Class";
    WNDCLASSEXW wc;
    memset(&wc, 0, sizeof(WNDCLASSEXW));
    wc.cbSize = sizeof(WNDCLASSEXW);
    wc.lpfnWndProc = WndProc;
    wc.hInstance = hInstance;
    wc.lpszClassName = CLASS_NAME;

    RegisterClassExW(&wc);

    HWND hwnd = CreateWindowExW(
        0,
        CLASS_NAME,
        L"SmartTaskbar Daemon",
        WS_OVERLAPPEDWINDOW,
        CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT,
        NULL, NULL, hInstance, NULL
    );

    if (!hwnd) {
        daemon_log("wWinMain: CreateWindowExW failed (error %lu)", GetLastError());
        ReleaseMutex(g_hDaemonMutex);
        CloseHandle(g_hDaemonMutex);
        return 1;
    }
    daemon_log("wWinMain: window created successfully: %p", (void*)hwnd);

    // 4. Load Settings & Initialize Screen Effect
    settings_load(&g_settings);
    if (g_settings.is_negative_enabled) {
        mag_apply_effect(g_settings.active_color_effect);
    }

    // 5. Initialize Taskbar Engine, System Tray, Brightness & Hotkeys
    daemon_log("wWinMain: initializing taskbar engine");
    taskbar_engine_init(hwnd);
    daemon_log("wWinMain: initializing tray");
    tray_init(hwnd, hInstance);
    daemon_log("wWinMain: initializing brightness");
    brightness_init(hwnd);
    daemon_log("wWinMain: initializing flyout");
    flyout_init(hInstance, hwnd, &g_settings);
    daemon_log("wWinMain: initializing hotkeys");
    hotkeys_init(hwnd);

    // 6. Register Named Event Listener for Settings Reload
    g_hReloadEvent = CreateEventW(NULL, FALSE, FALSE, SETTINGS_RELOAD_EVENT_NAME);
    if (g_hReloadEvent) {
        RegisterWaitForSingleObject(&g_hWaitHandle, g_hReloadEvent, settings_reload_callback, hwnd, INFINITE, WT_EXECUTEDEFAULT);
    }

    // 7. Message Loop
    daemon_log("wWinMain: entering message loop");
    MSG msg;
    while (GetMessageW(&msg, NULL, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }
    daemon_log("wWinMain: message loop exited with code %d", (int)msg.wParam);

    // 8. Clean up mutex
    if (g_hDaemonMutex) {
        ReleaseMutex(g_hDaemonMutex);
        CloseHandle(g_hDaemonMutex);
        g_hDaemonMutex = NULL;
    }

    return (int)msg.wParam;
}

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nShowCmd) {
    (void)lpCmdLine;
    return wWinMain(hInstance, hPrevInstance, GetCommandLineW(), nShowCmd);
}
