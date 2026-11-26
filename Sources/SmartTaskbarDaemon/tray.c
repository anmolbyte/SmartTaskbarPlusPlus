#include "tray.h"
#include "resource.h"
#include "magnification_effect.h"
#include "taskbar_engine.h"
#include <shellapi.h>
#include <stdio.h>

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

    Shell_NotifyIconW(NIM_ADD, &g_nid);
}

void tray_cleanup(void) {
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
                SHELLEXECUTEINFOW sei;
                memset(&sei, 0, sizeof(SHELLEXECUTEINFOW));
                sei.cbSize = sizeof(SHELLEXECUTEINFOW);
                sei.fMask = SEE_MASK_FLAG_NO_UI | SEE_MASK_NOASYNC;
                sei.hwnd = hwnd;
                sei.lpVerb = L"open";
                sei.lpFile = target_exe;
                sei.lpParameters = L"--settings";
                sei.lpDirectory = exe_dir;
                sei.nShow = SW_SHOWNORMAL;
                ShellExecuteExW(&sei);
            }
        }
    }
}

static void show_tray_menu(HWND hwnd, AppSettings *settings) {
    HMENU hMenu = CreatePopupMenu();
    if (!hMenu) return;

    // Auto Mode Checkbox
    UINT autoFlags = MF_STRING | (settings->auto_mode == AUTO_MODE_AUTO ? MF_CHECKED : MF_UNCHECKED);
    AppendMenuW(hMenu, autoFlags, IDM_AUTO_MODE, L"&Auto Mode");

    // Hide Taskbar when Fullscreen Checkbox
    UINT fsFlags = MF_STRING | (settings->hide_taskbar_when_fullscreen ? MF_CHECKED : MF_UNCHECKED);
    AppendMenuW(hMenu, fsFlags, IDM_HIDE_WHEN_FULLSCREEN, L"&Hide when Fullscreen");

    AppendMenuW(hMenu, MF_SEPARATOR, 0, NULL);

    // Screen Effects Submenu
    HMENU hSubEffects = CreatePopupMenu();
    if (hSubEffects) {
        // Toggle Active
        UINT toggleFlags = MF_STRING | (settings->is_negative_enabled ? MF_CHECKED : MF_UNCHECKED);
        AppendMenuW(hSubEffects, toggleFlags, IDM_EFFECT_TOGGLE, L"&Enabled");
        AppendMenuW(hSubEffects, MF_SEPARATOR, 0, NULL);

        // List of effect options with radio check for active
        struct {
            UINT id;
            const wchar_t *title;
            const char *name;
        } effects[] = {
            { IDM_EFFECT_NEGATIVE,       L"Negative",             "Negative" },
            { IDM_EFFECT_SMART_1,        L"Smart Invert 1",       "NegativeHueShift180Variation1" },
            { IDM_EFFECT_SMART_2,        L"Smart Invert 2",       "NegativeHueShift180Variation2" },
            { IDM_EFFECT_SMART_3,        L"Smart Invert 3",       "NegativeHueShift180Variation3" },
            { IDM_EFFECT_SMART_4,        L"Smart Invert 4",       "NegativeHueShift180Variation4" },
            { IDM_EFFECT_GRAYSCALE,      L"Grayscale",            "GrayScale" },
            { IDM_EFFECT_SEPIA,          L"Sepia",                "Sepia" },
            { IDM_EFFECT_RED,            L"Night Red",            "Red" },
            { IDM_EFFECT_HUESHIFT,       L"Hue Shift",            "HueShift180" },
            { IDM_EFFECT_NEG_GRAYSCALE,  L"Negative Grayscale",   "NegativeGrayScale" },
            { IDM_EFFECT_NEG_SEPIA,      L"Negative Sepia",       "NegativeSepia" },
            { IDM_EFFECT_NEG_RED,        L"Negative Red",         "NegativeRed" },
            { IDM_EFFECT_NEG_HUESHIFT,   L"Negative Hue Shift",   "NegativeHueShift180" },
        };

        int numEffects = sizeof(effects) / sizeof(effects[0]);
        for (int i = 0; i < numEffects; i++) {
            UINT flags = MF_STRING;
            if (strcmp(settings->active_color_effect, effects[i].name) == 0) {
                flags |= MF_CHECKED;
            }
            AppendMenuW(hSubEffects, flags, effects[i].id, effects[i].title);
        }

        AppendMenuW(hMenu, MF_POPUP, (UINT_PTR)hSubEffects, L"Screen &Effects");
    }

    AppendMenuW(hMenu, MF_SEPARATOR, 0, NULL);
    AppendMenuW(hMenu, MF_STRING, IDM_SETTINGS, L"&Settings...");
    AppendMenuW(hMenu, MF_STRING, IDM_EXIT, L"E&xit");

    POINT pt;
    GetCursorPos(&pt);

    // Required Win32 sequence to make context menu disappear when clicking outside
    SetForegroundWindow(hwnd);
    int cmd = TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_NONOTIFY | TPM_RIGHTBUTTON, pt.x, pt.y, hwnd, NULL);
    PostMessageW(hwnd, WM_NULL, 0, 0);

    DestroyMenu(hMenu);

    if (cmd == 0) return;

    switch (cmd) {
        case IDM_AUTO_MODE:
            settings->auto_mode = (settings->auto_mode == AUTO_MODE_AUTO ? AUTO_MODE_NONE : AUTO_MODE_AUTO);
            if (settings->auto_mode == AUTO_MODE_NONE) {
                taskbar_engine_restore(true);
            }
            settings_save(settings);
            break;

        case IDM_HIDE_WHEN_FULLSCREEN:
            settings->hide_taskbar_when_fullscreen = !settings->hide_taskbar_when_fullscreen;
            settings_save(settings);
            break;

        case IDM_EFFECT_TOGGLE:
            settings->is_negative_enabled = !settings->is_negative_enabled;
            if (settings->is_negative_enabled) {
                mag_apply_effect(settings->active_color_effect);
            } else {
                mag_restore_default();
            }
            settings_save(settings);
            break;

        case IDM_SETTINGS:
            tray_launch_settings(hwnd);
            break;

        case IDM_EXIT:
            DestroyWindow(hwnd);
            break;

        default:
            // Check if one of the screen effects
            {
                struct {
                    UINT id;
                    const char *name;
                } effMap[] = {
                    { IDM_EFFECT_NEGATIVE,      "Negative" },
                    { IDM_EFFECT_SMART_1,       "NegativeHueShift180Variation1" },
                    { IDM_EFFECT_SMART_2,       "NegativeHueShift180Variation2" },
                    { IDM_EFFECT_SMART_3,       "NegativeHueShift180Variation3" },
                    { IDM_EFFECT_SMART_4,       "NegativeHueShift180Variation4" },
                    { IDM_EFFECT_GRAYSCALE,     "GrayScale" },
                    { IDM_EFFECT_SEPIA,         "Sepia" },
                    { IDM_EFFECT_RED,           "Red" },
                    { IDM_EFFECT_HUESHIFT,      "HueShift180" },
                    { IDM_EFFECT_NEG_GRAYSCALE, "NegativeGrayScale" },
                    { IDM_EFFECT_NEG_SEPIA,     "NegativeSepia" },
                    { IDM_EFFECT_NEG_RED,       "NegativeRed" },
                    { IDM_EFFECT_NEG_HUESHIFT,  "NegativeHueShift180" },
                };
                for (size_t i = 0; i < sizeof(effMap) / sizeof(effMap[0]); i++) {
                    if (effMap[i].id == (UINT)cmd) {
                        strncpy(settings->active_color_effect, effMap[i].name, sizeof(settings->active_color_effect) - 1);
                        settings->active_color_effect[sizeof(settings->active_color_effect) - 1] = '\0';
                        settings->is_negative_enabled = true;
                        mag_apply_effect(settings->active_color_effect);
                        settings_save(settings);
                        break;
                    }
                }
            }
            break;
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
                handle_tray_click(settings);
                break;
            case WM_RBUTTONUP:
                show_tray_menu(hwnd, settings);
                break;
            case WM_LBUTTONDBLCLK:
                tray_launch_settings(hwnd);
                break;
            default:
                break;
        }
    }
}
