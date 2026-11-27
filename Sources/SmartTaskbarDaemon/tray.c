#include "tray.h"
#include "resource.h"
#include "magnification_effect.h"
#include "taskbar_engine.h"
#include "flyout.h"
#include "brightness.h"
#include <shellapi.h>
#include <stdio.h>

void daemon_log(const char *fmt, ...);

static NOTIFYICONDATAW g_nid;
static HWND g_hwnd = NULL;
static HINSTANCE g_hInstance = NULL;
static UINT g_uTaskbarRestartMsg = 0;

UINT tray_get_taskbar_restart_msg(void) {
    return g_uTaskbarRestartMsg;
}

static bool is_system_light_theme(void) {
    HKEY hKey;
    DWORD value = 0;
    DWORD size = sizeof(value);
    if (RegOpenKeyExW(HKEY_CURRENT_USER,
                      L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
                      0, KEY_READ, &hKey) == ERROR_SUCCESS) {
        if (RegQueryValueExW(hKey, L"SystemUsesLightTheme", NULL, NULL, (LPBYTE)&value, &size) == ERROR_SUCCESS) {
            RegCloseKey(hKey);
            return (value != 0);
        }
        RegCloseKey(hKey);
    }
    return false;
}

static HICON get_tray_icon(void) {
    int iconId = is_system_light_theme() ? IDI_LOGO_BLACK : IDI_LOGO_WHITE;
    return (HICON)LoadImageW(g_hInstance, MAKEINTRESOURCEW(iconId), IMAGE_ICON,
                             GetSystemMetrics(SM_CXSMICON), GetSystemMetrics(SM_CYSMICON),
                             LR_DEFAULTCOLOR);
}

void tray_update_icon(void) {
    if (!g_hwnd) return;
    HICON hOldIcon = g_nid.hIcon;
    g_nid.hIcon = get_tray_icon();
    g_nid.uFlags = NIF_ICON;
    Shell_NotifyIconW(NIM_MODIFY, &g_nid);
    if (hOldIcon) DestroyIcon(hOldIcon);
}

void tray_init(HWND hwnd, HINSTANCE hInstance) {
    g_hwnd = hwnd;
    g_hInstance = hInstance;
    g_uTaskbarRestartMsg = RegisterWindowMessageW(L"TaskbarCreated");

    memset(&g_nid, 0, sizeof(NOTIFYICONDATAW));
    g_nid.cbSize = sizeof(NOTIFYICONDATAW);
    g_nid.hWnd = hwnd;
    g_nid.uID = 1;
    g_nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
    g_nid.uCallbackMessage = WM_APP_TRAYMSG;
    g_nid.hIcon = get_tray_icon();
    wcsncpy(g_nid.szTip, L"SmartTaskbar++", sizeof(g_nid.szTip) / sizeof(wchar_t) - 1);

    BOOL res = FALSE;
    for (int attempt = 0; attempt < 5; attempt++) {
        Shell_NotifyIconW(NIM_DELETE, &g_nid);
        res = Shell_NotifyIconW(NIM_ADD, &g_nid);
        daemon_log("tray_init: attempt %d, hIcon=%p, NIM_ADD result=%d, GetLastError=%lu", attempt + 1, g_nid.hIcon, res, GetLastError());
        if (res) break;
        Sleep(200);
    }
}

void tray_cleanup(void) {
    flyout_cleanup();
    if (g_nid.hWnd) {
        Shell_NotifyIconW(NIM_DELETE, &g_nid);
        if (g_nid.hIcon) {
            DestroyIcon(g_nid.hIcon);
            g_nid.hIcon = NULL;
        }
        g_nid.hWnd = NULL;
    }
}

void tray_launch_settings(HWND hwnd) {
    (void)hwnd;
    // 1. Check if existing Settings window is already visible/running
    HWND hExisting = FindWindowW(NULL, L"SmartTaskbar++ Settings");
    if (hExisting) {
        daemon_log("tray_launch_settings: bringing existing window %p to front", (void*)hExisting);
        ShowWindow(hExisting, SW_RESTORE);
        SetForegroundWindow(hExisting);
        return;
    }

    // 2. Find executable in same directory: SmartTaskbarSettings.exe or SmartTaskbar++.exe
    wchar_t exe_dir[MAX_PATH];
    if (GetModuleFileNameW(NULL, exe_dir, MAX_PATH) > 0) {
        wchar_t *last_slash = wcsrchr(exe_dir, L'\\');
        if (last_slash) {
            *last_slash = L'\0';

            wchar_t target_exe[MAX_PATH];
            swprintf(target_exe, MAX_PATH, L"%ls\\SmartTaskbarSettings.exe", exe_dir);

            if (GetFileAttributesW(target_exe) == INVALID_FILE_ATTRIBUTES) {
                // Try fallback SmartTaskbar++.exe
                swprintf(target_exe, MAX_PATH, L"%ls\\SmartTaskbar++.exe", exe_dir);
            }

            if (GetFileAttributesW(target_exe) != INVALID_FILE_ATTRIBUTES) {
                wchar_t cmd_line[MAX_PATH + 32];
                swprintf(cmd_line, MAX_PATH + 32, L"\"%ls\" --settings", target_exe);

                STARTUPINFOW si;
                PROCESS_INFORMATION pi;
                memset(&si, 0, sizeof(si));
                si.cb = sizeof(si);
                si.lpDesktop = L"WinSta0\\Default";
                si.dwFlags = STARTF_USESHOWWINDOW;
                si.wShowWindow = SW_SHOWNORMAL;
                memset(&pi, 0, sizeof(pi));

                AllowSetForegroundWindow(ASFW_ANY);
                BOOL ok = CreateProcessW(target_exe, cmd_line, NULL, NULL, FALSE, 0, NULL, exe_dir, &si, &pi);
                daemon_log("tray_launch_settings: CreateProcessW('%ls') ok=%d err=%lu pid=%lu",
                           target_exe, ok, ok ? 0 : GetLastError(), ok ? pi.dwProcessId : 0);
                if (ok) {
                    CloseHandle(pi.hProcess);
                    CloseHandle(pi.hThread);
                }
            } else {
                daemon_log("tray_launch_settings: executable not found at '%ls'", target_exe);
            }
        }
    }
}

static void handle_tray_click(AppSettings *settings) {
    switch (settings->click_action) {
        case CLICK_ACTION_TOGGLE_INVERSION:
            settings->is_negative_enabled = !settings->is_negative_enabled;
            if (settings->is_negative_enabled) {
                mag_apply_effect(settings->active_color_effect);
            } else {
                mag_restore_default();
            }
            settings_save(settings);
            break;

        case CLICK_ACTION_TOGGLE_AUTO_MODE:
            settings->auto_mode = (settings->auto_mode == AUTO_MODE_AUTO ? AUTO_MODE_NONE : AUTO_MODE_AUTO);
            if (settings->auto_mode == AUTO_MODE_NONE) {
                taskbar_engine_restore(true);
            }
            settings_save(settings);
            break;

        case CLICK_ACTION_SET_SHOW_MODE:
            settings->auto_mode = AUTO_MODE_NONE;
            taskbar_engine_restore(true);
            settings_save(settings);
            break;

        case CLICK_ACTION_SET_HIDE_MODE:
            settings->auto_mode = AUTO_MODE_NONE;
            taskbar_engine_restore(false);
            settings_save(settings);
            break;

        case CLICK_ACTION_NONE:
        default:
            break;
    }
}

void tray_on_message(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam, AppSettings *settings) {
    (void)wParam;
    if (g_uTaskbarRestartMsg != 0 && uMsg == g_uTaskbarRestartMsg) {
        // Explorer restarted: re-register notify icon
        tray_init(hwnd, g_hInstance);
        return;
    }

    if (uMsg == WM_APP_TRAYMSG) {
        switch (LOWORD(lParam)) {
            case WM_LBUTTONUP:
                daemon_log("tray: WM_LBUTTONUP (click_action=%d)", (int)settings->click_action);
                handle_tray_click(settings);
                break;
            case WM_RBUTTONUP:
                daemon_log("tray: WM_RBUTTONUP -> opening unified native menu");
                SetForegroundWindow(hwnd);
                flyout_toggle(hwnd);
                PostMessageW(hwnd, WM_NULL, 0, 0);
                break;
            case WM_LBUTTONDBLCLK:
                daemon_log("tray: WM_LBUTTONDBLCLK -> opening Settings");
                tray_launch_settings(hwnd);
                break;
            default:
                break;
        }
    }
}
