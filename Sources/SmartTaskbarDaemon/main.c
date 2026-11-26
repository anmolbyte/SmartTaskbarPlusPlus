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

#define IDT_ENGINE_TIMER 1

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
            return 0;

        case WM_SETTINGCHANGE:
            tray_update_icon();
            return 0;

        case WM_DISPLAYCHANGE:
            taskbar_engine_invalidate_cache();
            return 0;

        case WM_QUERYENDSESSION:
            return TRUE;

        case WM_ENDSESSION:
            if (wParam) {
                KillTimer(hwnd, IDT_ENGINE_TIMER);
                taskbar_engine_stop(&g_settings);
                mag_uninit();
                tray_cleanup();
            }
            return 0;

        case WM_CLOSE:
            DestroyWindow(hwnd);
            return 0;

        case WM_DESTROY:
            KillTimer(hwnd, IDT_ENGINE_TIMER);
            if (g_hWaitHandle) {
                UnregisterWaitEx(g_hWaitHandle, INVALID_HANDLE_VALUE);
                g_hWaitHandle = NULL;
            }
            if (g_hReloadEvent) {
                CloseHandle(g_hReloadEvent);
                g_hReloadEvent = NULL;
            }
            taskbar_engine_stop(&g_settings);
            mag_uninit();
            tray_cleanup();
            PostQuitMessage(0);
            return 0;
    }

    return DefWindowProcW(hwnd, uMsg, wParam, lParam);
}

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPWSTR lpCmdLine, int nShowCmd) {
    (void)hPrevInstance; (void)lpCmdLine; (void)nShowCmd;

    // 1. Single Instance Check
    g_hDaemonMutex = CreateMutexW(NULL, TRUE, DAEMON_MUTEX_NAME);
    if (!g_hDaemonMutex || GetLastError() == ERROR_ALREADY_EXISTS) {
        if (g_hDaemonMutex) CloseHandle(g_hDaemonMutex);
        return 0; // Daemon is already running
    }

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
        ReleaseMutex(g_hDaemonMutex);
        CloseHandle(g_hDaemonMutex);
        return 1;
    }

    // 4. Load Settings & Initialize Screen Effect
    settings_load(&g_settings);
    if (g_settings.is_negative_enabled) {
        mag_apply_effect(g_settings.active_color_effect);
    }

    // 5. Initialize Taskbar Engine & System Tray
    taskbar_engine_init(hwnd);
    tray_init(hwnd, hInstance);

    // 6. Register Named Event Listener for Settings Reload
    g_hReloadEvent = CreateEventW(NULL, FALSE, FALSE, SETTINGS_RELOAD_EVENT_NAME);
    if (g_hReloadEvent) {
        RegisterWaitForSingleObject(&g_hWaitHandle, g_hReloadEvent, settings_reload_callback, hwnd, INFINITE, WT_EXECUTEDEFAULT);
    }

    // 7. Message Loop
    MSG msg;
    while (GetMessageW(&msg, NULL, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

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
