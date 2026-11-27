#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <commctrl.h>
#include <dwmapi.h>
#include <stdio.h>
#include <string.h>
#include <stdbool.h>
#include "flyout.h"
#include "brightness.h"
#include "magnification_effect.h"
#include "taskbar_engine.h"
#include "resource.h"

void daemon_log(const char *fmt, ...);

#define FLYOUT_CLASS_NAME  L"SmartTaskbar_Brightness_Flyout_Class"
#define SUBMENU_CLASS_NAME L"SmartTaskbar_Submenu_Flyout_Class"
#define MAX_MENU_ROWS      24

typedef struct {
    UINT id;
    wchar_t text[48];
    bool is_separator;
    bool is_checked;
    bool is_bold;
    bool has_submenu;
    RECT rect;
} MenuRow;

typedef struct {
    HWND hLabelName;
    HWND hLabelVal;
    HWND hTrackbar;
    int monitor_index;
} MonitorControls;

typedef struct {
    UINT id;
    const wchar_t *title;
    const char *effect_name;
} EffectDef;

static const EffectDef g_effectDefs[] = {
    { IDM_EFFECT_NEGATIVE,      L"Negative",           "Negative" },
    { IDM_EFFECT_SMART_1,       L"Smart Invert 1",     "NegativeHueShift180Variation1" },
    { IDM_EFFECT_SMART_2,       L"Smart Invert 2",     "NegativeHueShift180Variation2" },
    { IDM_EFFECT_SMART_3,       L"Smart Invert 3",     "NegativeHueShift180Variation3" },
    { IDM_EFFECT_SMART_4,       L"Smart Invert 4",     "NegativeHueShift180Variation4" },
    { IDM_EFFECT_GRAYSCALE,     L"Grayscale",          "GrayScale" },
    { IDM_EFFECT_SEPIA,         L"Sepia",              "Sepia" },
    { IDM_EFFECT_RED,           L"Night Red",          "Red" },
    { IDM_EFFECT_HUESHIFT,      L"Hue Shift",          "HueShift180" },
    { IDM_EFFECT_NEG_GRAYSCALE, L"Negative Grayscale", "NegativeGrayScale" },
    { IDM_EFFECT_NEG_SEPIA,     L"Negative Sepia",     "NegativeSepia" },
    { IDM_EFFECT_NEG_RED,       L"Negative Red",       "NegativeRed" },
    { IDM_EFFECT_NEG_HUESHIFT,  L"Negative Hue Shift", "NegativeHueShift180" },
};

static HWND g_hFlyout = NULL;
static HWND g_hSubmenu = NULL;
static HINSTANCE g_hInstance = NULL;
static HWND g_hDaemonWnd = NULL;
static AppSettings *g_settings = NULL;
static ULONGLONG g_lastShowTick = 0;

static HFONT g_hFontNormal = NULL;
static HFONT g_hFontBold = NULL;
static HBRUSH g_hBgBrush = NULL;
static HBRUSH g_hHoverBrush = NULL;
static COLORREF g_textColor = RGB(242, 242, 242);
static COLORREF g_subTextColor = RGB(175, 175, 175);
static COLORREF g_bgColor = RGB(32, 32, 32);
static COLORREF g_hoverColor = RGB(52, 52, 52);
static COLORREF g_borderColor = RGB(62, 62, 62);
static COLORREF g_sepColor = RGB(56, 56, 56);
static bool g_isDarkTheme = true;
static UINT g_currentDpi = 96;

static MonitorControls g_controls[MAX_APP_MONITORS];
static int g_controlCount = 0;

static MenuRow g_mainRows[MAX_MENU_ROWS];
static int g_mainRowCount = 0;
static int g_mainHovered = -1;
static bool g_mainTrackingLeave = false;

static MenuRow g_subRows[MAX_MENU_ROWS];
static int g_subRowCount = 0;
static int g_subHovered = -1;
static bool g_subTrackingLeave = false;

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

static UINT get_monitor_dpi(HMONITOR hMon) {
    if (hMon) {
        HMODULE hShcore = LoadLibraryW(L"shcore.dll");
        if (hShcore) {
            typedef HRESULT (WINAPI *GetDpiForMonitorFn)(HMONITOR, int, UINT*, UINT*);
            GetDpiForMonitorFn fn = (GetDpiForMonitorFn)(void *)GetProcAddress(hShcore, "GetDpiForMonitor");
            if (fn) {
                UINT dpiX = 96, dpiY = 96;
                if (SUCCEEDED(fn(hMon, 0 /* MDT_EFFECTIVE_DPI */, &dpiX, &dpiY)) && dpiX > 0) {
                    FreeLibrary(hShcore);
                    return dpiX;
                }
            }
            FreeLibrary(hShcore);
        }
    }
    HDC hdc = GetDC(NULL);
    UINT dpi = (UINT)GetDeviceCaps(hdc, LOGPIXELSX);
    ReleaseDC(NULL, hdc);
    return dpi ? dpi : 96;
}

static int dip(int px, UINT dpi) {
    return MulDiv(px, (int)dpi, 96);
}

static void format_brightness_text(int val, wchar_t *buf, size_t max_chars) {
    if (g_settings && g_settings->use_points_for_brightness) {
        swprintf(buf, max_chars, L"%d pts", val);
    } else {
        swprintf(buf, max_chars, L"%d%%", val);
    }
}

static void update_theme_colors(void) {
    g_isDarkTheme = !is_system_light_theme();
    if (g_isDarkTheme) {
        g_bgColor = RGB(32, 32, 32);
        g_hoverColor = RGB(52, 52, 52);
        g_borderColor = RGB(62, 62, 62);
        g_sepColor = RGB(56, 56, 56);
        g_textColor = RGB(242, 242, 242);
        g_subTextColor = RGB(175, 175, 175);
    } else {
        g_bgColor = RGB(248, 248, 248);
        g_hoverColor = RGB(230, 230, 230);
        g_borderColor = RGB(205, 205, 205);
        g_sepColor = RGB(222, 222, 222);
        g_textColor = RGB(20, 20, 20);
        g_subTextColor = RGB(95, 95, 95);
    }

    if (g_hBgBrush) DeleteObject(g_hBgBrush);
    if (g_hHoverBrush) DeleteObject(g_hHoverBrush);
    g_hBgBrush = CreateSolidBrush(g_bgColor);
    g_hHoverBrush = CreateSolidBrush(g_hoverColor);
}

static void apply_dwm_popup_style(HWND hwnd) {
    DWORD dval = 2; // DWMWCP_ROUND
    DwmSetWindowAttribute(hwnd, 33, &dval, sizeof(dval));
    BOOL dm = g_isDarkTheme ? TRUE : FALSE;
    DwmSetWindowAttribute(hwnd, 20, &dm, sizeof(dm));
}

static void create_fonts(UINT dpi) {
    if (g_hFontNormal) DeleteObject(g_hFontNormal);
    if (g_hFontBold) DeleteObject(g_hFontBold);

    int textHeight = -MulDiv(10, (int)dpi, 72);
    g_hFontNormal = CreateFontW(textHeight, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
                                DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
                                CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");
    g_hFontBold = CreateFontW(textHeight, 0, 0, 0, FW_SEMIBOLD, FALSE, FALSE, FALSE,
                              DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
                              CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");
}

static void clear_controls(void) {
    for (int i = 0; i < g_controlCount; i++) {
        if (g_controls[i].hLabelName) DestroyWindow(g_controls[i].hLabelName);
        if (g_controls[i].hLabelVal) DestroyWindow(g_controls[i].hLabelVal);
        if (g_controls[i].hTrackbar) DestroyWindow(g_controls[i].hTrackbar);
    }
    g_controlCount = 0;
}

static void add_menu_row(MenuRow *rows, int *count, UINT id, const wchar_t *text,
                         bool is_sep, bool is_checked, bool is_bold, bool has_submenu,
                         int width, int *curY, UINT dpi) {
    if (*count >= MAX_MENU_ROWS) return;
    MenuRow *r = &rows[*count];
    memset(r, 0, sizeof(MenuRow));
    r->id = id;
    r->is_separator = is_sep;
    r->is_checked = is_checked;
    r->is_bold = is_bold;
    r->has_submenu = has_submenu;
    if (text) {
        wcsncpy(r->text, text, (sizeof(r->text) / sizeof(wchar_t)) - 1);
    }

    int rowH = is_sep ? dip(9, dpi) : dip(32, dpi);
    int insetX = dip(5, dpi);
    r->rect.left = insetX;
    r->rect.top = *curY;
    r->rect.right = width - insetX;
    r->rect.bottom = *curY + rowH;

    *curY += rowH;
    (*count)++;
}

static int hit_test_rows(const MenuRow *rows, int count, POINT pt) {
    for (int i = 0; i < count; i++) {
        if (!rows[i].is_separator && PtInRect(&rows[i].rect, pt)) {
            return i;
        }
    }
    return -1;
}

static void draw_popup_surface(HWND hwnd, HDC hdc, const MenuRow *rows, int rowCount, int hoveredIdx, UINT dpi) {
    RECT rc;
    GetClientRect(hwnd, &rc);
    FillRect(hdc, &rc, g_hBgBrush);

    SetBkMode(hdc, TRANSPARENT);
    SetTextColor(hdc, g_textColor);

    int padText = dip(32, dpi);
    int checkCenterX = dip(18, dpi);
    int chevCenterX = (rc.right - rc.left) - dip(16, dpi);
    int radius = dip(5, dpi);
    int penW = dip(15, dpi) / 10;
    if (penW < 1) penW = 1;

    HPEN hSepPen = CreatePen(PS_SOLID, 1, g_sepColor);
    HPEN hGlyphPen = CreatePen(PS_SOLID, penW, g_textColor);
    HPEN hHoverPen = CreatePen(PS_SOLID, 1, g_hoverColor);

    for (int i = 0; i < rowCount; i++) {
        const MenuRow *r = &rows[i];
        if (r->is_separator) {
            int midY = (r->rect.top + r->rect.bottom) / 2;
            HPEN oldP = (HPEN)SelectObject(hdc, hSepPen);
            MoveToEx(hdc, dip(10, dpi), midY, NULL);
            LineTo(hdc, rc.right - dip(10, dpi), midY);
            SelectObject(hdc, oldP);
            continue;
        }

        if (i == hoveredIdx) {
            HBRUSH oldB = (HBRUSH)SelectObject(hdc, g_hHoverBrush);
            HPEN oldP = (HPEN)SelectObject(hdc, hHoverPen);
            RoundRect(hdc, r->rect.left, r->rect.top, r->rect.right, r->rect.bottom, radius, radius);
            SelectObject(hdc, oldP);
            SelectObject(hdc, oldB);
        }

        int midY = (r->rect.top + r->rect.bottom) / 2;

        if (r->is_checked) {
            HPEN oldP = (HPEN)SelectObject(hdc, hGlyphPen);
            MoveToEx(hdc, checkCenterX - dip(4, dpi), midY, NULL);
            LineTo(hdc, checkCenterX - dip(1, dpi), midY + dip(3, dpi));
            LineTo(hdc, checkCenterX + dip(5, dpi), midY - dip(3, dpi));
            SelectObject(hdc, oldP);
        }

        HFONT hFont = (r->is_bold && g_hFontBold) ? g_hFontBold : g_hFontNormal;
        HFONT oldF = (HFONT)SelectObject(hdc, hFont);
        RECT tr = r->rect;
        tr.left += padText - dip(4, dpi);
        tr.right -= r->has_submenu ? dip(24, dpi) : dip(10, dpi);
        DrawTextW(hdc, r->text, -1, &tr, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);
        SelectObject(hdc, oldF);

        if (r->has_submenu) {
            HPEN oldP = (HPEN)SelectObject(hdc, hGlyphPen);
            MoveToEx(hdc, chevCenterX - dip(2, dpi), midY - dip(4, dpi), NULL);
            LineTo(hdc, chevCenterX + dip(2, dpi), midY);
            LineTo(hdc, chevCenterX - dip(2, dpi), midY + dip(4, dpi));
            SelectObject(hdc, oldP);
        }
    }

    HPEN hBorderPen = CreatePen(PS_SOLID, 1, g_borderColor);
    HPEN oldBorder = (HPEN)SelectObject(hdc, hBorderPen);
    HBRUSH oldBrush = (HBRUSH)SelectObject(hdc, GetStockObject(NULL_BRUSH));
    Rectangle(hdc, rc.left, rc.top, rc.right, rc.bottom);
    SelectObject(hdc, oldBrush);
    SelectObject(hdc, oldBorder);

    DeleteObject(hBorderPen);
    DeleteObject(hHoverPen);
    DeleteObject(hGlyphPen);
    DeleteObject(hSepPen);
}

static bool is_popup_window_or_child(HWND target) {
    if (!target) return false;
    if (target == g_hFlyout || target == g_hSubmenu) return true;
    if (g_hFlyout && IsChild(g_hFlyout, target)) return true;
    if (g_hSubmenu && IsChild(g_hSubmenu, target)) return true;
    return false;
}

static void submenu_hide(void) {
    if (g_hSubmenu && IsWindowVisible(g_hSubmenu)) {
        ShowWindow(g_hSubmenu, SW_HIDE);
    }
    g_subHovered = -1;
}

static void build_submenu_rows(int width, UINT dpi) {
    g_subRowCount = 0;
    int curY = dip(6, dpi);
    bool enabled = g_settings ? g_settings->is_negative_enabled : false;
    const char *activeEff = g_settings ? g_settings->active_color_effect : "";

    add_menu_row(g_subRows, &g_subRowCount, IDM_EFFECT_TOGGLE, L"Enabled", false, enabled, true, false, width, &curY, dpi);
    add_menu_row(g_subRows, &g_subRowCount, 0, NULL, true, false, false, false, width, &curY, dpi);

    int numEffects = (int)(sizeof(g_effectDefs) / sizeof(g_effectDefs[0]));
    for (int i = 0; i < numEffects; i++) {
        bool checked = (strcmp(activeEff, g_effectDefs[i].effect_name) == 0);
        add_menu_row(g_subRows, &g_subRowCount, g_effectDefs[i].id, g_effectDefs[i].title,
                     false, checked, false, false, width, &curY, dpi);
    }
}

static void show_screen_effects_submenu(int anchorRowIdx) {
    if (!g_hFlyout || anchorRowIdx < 0 || anchorRowIdx >= g_mainRowCount) return;

    UINT dpi = g_currentDpi;
    int subWidth = dip(216, dpi);
    build_submenu_rows(subWidth, dpi);
    int subHeight = g_subRows[g_subRowCount - 1].rect.bottom + dip(6, dpi);

    if (!g_hSubmenu) {
        g_hSubmenu = CreateWindowExW(
            WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE,
            SUBMENU_CLASS_NAME,
            L"SmartTaskbar++ Screen Effects",
            WS_POPUP | WS_CLIPCHILDREN,
            0, 0, subWidth, subHeight,
            g_hFlyout, NULL, g_hInstance, NULL
        );
        if (!g_hSubmenu) return;
    }
    apply_dwm_popup_style(g_hSubmenu);

    RECT rcMain;
    GetWindowRect(g_hFlyout, &rcMain);
    POINT rowTopLeft = { 0, g_mainRows[anchorRowIdx].rect.top };
    ClientToScreen(g_hFlyout, &rowTopLeft);

    HMONITOR hMon = MonitorFromWindow(g_hFlyout, MONITOR_DEFAULTTONEAREST);
    MONITORINFO mi;
    memset(&mi, 0, sizeof(mi));
    mi.cbSize = sizeof(mi);
    GetMonitorInfoW(hMon, &mi);

    int overlap = dip(4, dpi);
    int x = rcMain.right - overlap;
    if (x + subWidth > mi.rcWork.right - dip(6, dpi)) {
        x = rcMain.left - subWidth + overlap;
    }
    if (x < mi.rcWork.left + dip(6, dpi)) {
        x = mi.rcWork.left + dip(6, dpi);
    }

    int y = rowTopLeft.y - dip(6, dpi);
    if (y + subHeight > mi.rcWork.bottom - dip(6, dpi)) {
        y = mi.rcWork.bottom - subHeight - dip(6, dpi);
    }
    if (y < mi.rcWork.top + dip(6, dpi)) {
        y = mi.rcWork.top + dip(6, dpi);
    }

    g_subHovered = -1;
    SetWindowPos(g_hSubmenu, HWND_TOPMOST, x, y, subWidth, subHeight, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    InvalidateRect(g_hSubmenu, NULL, FALSE);
}

static void execute_menu_command(UINT cmd) {
    if (!cmd) return;

    if (cmd == IDM_SCREEN_EFFECTS_SUBMENU) {
        for (int i = 0; i < g_mainRowCount; i++) {
            if (g_mainRows[i].id == IDM_SCREEN_EFFECTS_SUBMENU) {
                if (g_hSubmenu && IsWindowVisible(g_hSubmenu)) {
                    submenu_hide();
                } else {
                    show_screen_effects_submenu(i);
                }
                break;
            }
        }
        return;
    }

    flyout_hide();

    switch (cmd) {
        case IDM_AUTO_MODE:
            if (g_settings) {
                g_settings->auto_mode = (g_settings->auto_mode == AUTO_MODE_AUTO ? AUTO_MODE_NONE : AUTO_MODE_AUTO);
                if (g_settings->auto_mode == AUTO_MODE_NONE) {
                    taskbar_engine_restore(true);
                }
                settings_save(g_settings);
            }
            break;

        case IDM_HIDE_WHEN_FULLSCREEN:
            if (g_settings) {
                g_settings->hide_taskbar_when_fullscreen = !g_settings->hide_taskbar_when_fullscreen;
                settings_save(g_settings);
            }
            break;

        case IDM_EFFECT_TOGGLE:
            if (g_settings) {
                g_settings->is_negative_enabled = !g_settings->is_negative_enabled;
                if (g_settings->is_negative_enabled) {
                    mag_apply_effect(g_settings->active_color_effect);
                } else {
                    mag_restore_default();
                }
                settings_save(g_settings);
            }
            break;

        case IDM_SETTINGS:
            AllowSetForegroundWindow(ASFW_ANY);
            PostMessageW(g_hDaemonWnd, WM_APP_SHOW_SETTINGS, 0, 0);
            break;

        case IDM_EXIT:
            PostMessageW(g_hDaemonWnd, WM_CLOSE, 0, 0);
            break;

        default: {
            int numEffects = (int)(sizeof(g_effectDefs) / sizeof(g_effectDefs[0]));
            for (int i = 0; i < numEffects; i++) {
                if (g_effectDefs[i].id == cmd && g_settings) {
                    strncpy(g_settings->active_color_effect, g_effectDefs[i].effect_name,
                            sizeof(g_settings->active_color_effect) - 1);
                    g_settings->active_color_effect[sizeof(g_settings->active_color_effect) - 1] = '\0';
                    g_settings->is_negative_enabled = true;
                    mag_apply_effect(g_settings->active_color_effect);
                    settings_save(g_settings);
                    break;
                }
            }
            break;
        }
    }
}

static void handle_hover_move(HWND hwnd, LPARAM lParam, MenuRow *rows, int rowCount,
                              int *hoveredIdx, bool *trackingLeave, bool isMainPopup) {
    if (!*trackingLeave) {
        TRACKMOUSEEVENT tme;
        memset(&tme, 0, sizeof(tme));
        tme.cbSize = sizeof(tme);
        tme.dwFlags = TME_LEAVE;
        tme.hwndTrack = hwnd;
        TrackMouseEvent(&tme);
        *trackingLeave = true;
    }

    POINT pt = { (short)LOWORD(lParam), (short)HIWORD(lParam) };
    int idx = hit_test_rows(rows, rowCount, pt);
    if (idx != *hoveredIdx) {
        *hoveredIdx = idx;
        InvalidateRect(hwnd, NULL, FALSE);

        if (isMainPopup && idx >= 0) {
            if (rows[idx].id == IDM_SCREEN_EFFECTS_SUBMENU) {
                show_screen_effects_submenu(idx);
            } else {
                submenu_hide();
            }
        }
    }
}

static LRESULT CALLBACK SubmenuWndProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam) {
    switch (uMsg) {
        case WM_MOUSEACTIVATE:
            return MA_NOACTIVATE;

        case WM_MOUSEMOVE:
            handle_hover_move(hwnd, lParam, g_subRows, g_subRowCount, &g_subHovered, &g_subTrackingLeave, false);
            return 0;

        case WM_MOUSELEAVE:
            g_subTrackingLeave = false;
            if (g_subHovered != -1) {
                g_subHovered = -1;
                InvalidateRect(hwnd, NULL, FALSE);
            }
            return 0;

        case WM_LBUTTONUP: {
            POINT pt = { (short)LOWORD(lParam), (short)HIWORD(lParam) };
            int idx = hit_test_rows(g_subRows, g_subRowCount, pt);
            if (idx >= 0) {
                execute_menu_command(g_subRows[idx].id);
            }
            return 0;
        }

        case WM_ERASEBKGND:
            return 1;

        case WM_PAINT: {
            PAINTSTRUCT ps;
            HDC hdc = BeginPaint(hwnd, &ps);
            draw_popup_surface(hwnd, hdc, g_subRows, g_subRowCount, g_subHovered, g_currentDpi);
            EndPaint(hwnd, &ps);
            return 0;
        }
    }
    return DefWindowProcW(hwnd, uMsg, wParam, lParam);
}

static LRESULT CALLBACK FlyoutWndProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam) {
    switch (uMsg) {
        case WM_ACTIVATE:
            daemon_log("flyout WM_ACTIVATE: wParam=0x%Ix", (size_t)wParam);
            if (LOWORD(wParam) == WA_INACTIVE) {
                HWND hOther = (HWND)lParam;
                if (is_popup_window_or_child(hOther) || is_popup_window_or_child(GetForegroundWindow())) {
                    return 0;
                }
                if (GetTickCount64() - g_lastShowTick < 200) {
                    daemon_log("flyout ignoring immediate WA_INACTIVE within 200ms of show");
                    return 0;
                }
                flyout_hide();
                return 0;
            }
            break;

        case WM_MOUSEMOVE:
            handle_hover_move(hwnd, lParam, g_mainRows, g_mainRowCount, &g_mainHovered, &g_mainTrackingLeave, true);
            return 0;

        case WM_MOUSELEAVE:
            g_mainTrackingLeave = false;
            if (g_mainHovered != -1) {
                g_mainHovered = -1;
                InvalidateRect(hwnd, NULL, FALSE);
            }
            return 0;

        case WM_LBUTTONUP: {
            POINT pt = { (short)LOWORD(lParam), (short)HIWORD(lParam) };
            int idx = hit_test_rows(g_mainRows, g_mainRowCount, pt);
            if (idx >= 0) {
                execute_menu_command(g_mainRows[idx].id);
            }
            return 0;
        }

        case WM_HSCROLL: {
            if (LOWORD(wParam) == SB_ENDSCROLL) {
                return 0;
            }
            HWND hTrack = (HWND)lParam;
            if (hTrack) {
                int mon_idx = (int)GetWindowLongPtrW(hTrack, GWLP_USERDATA);
                int pos = (int)SendMessageW(hTrack, TBM_GETPOS, 0, 0);

                for (int i = 0; i < g_controlCount; i++) {
                    if (g_controls[i].monitor_index == mon_idx && g_controls[i].hLabelVal) {
                        wchar_t buf[32];
                        format_brightness_text(pos, buf, sizeof(buf) / sizeof(wchar_t));
                        SetWindowTextW(g_controls[i].hLabelVal, buf);
                        break;
                    }
                }

                brightness_set_direct(mon_idx, pos);
            }
            return 0;
        }

        case WM_CTLCOLORSTATIC: {
            HDC hdc = (HDC)wParam;
            HWND hCtrl = (HWND)lParam;
            SetBkMode(hdc, TRANSPARENT);
            SetBkColor(hdc, g_bgColor);

            bool isVal = false;
            for (int i = 0; i < g_controlCount; i++) {
                if (g_controls[i].hLabelVal == hCtrl) {
                    isVal = true;
                    break;
                }
            }

            SetTextColor(hdc, isVal ? g_subTextColor : g_textColor);
            return (LRESULT)g_hBgBrush;
        }

        case WM_ERASEBKGND:
            return 1;

        case WM_PAINT: {
            PAINTSTRUCT ps;
            HDC hdc = BeginPaint(hwnd, &ps);
            draw_popup_surface(hwnd, hdc, g_mainRows, g_mainRowCount, g_mainHovered, g_currentDpi);
            EndPaint(hwnd, &ps);
            return 0;
        }

        case WM_KEYDOWN:
            if (wParam == VK_ESCAPE) {
                if (g_hSubmenu && IsWindowVisible(g_hSubmenu)) {
                    submenu_hide();
                } else {
                    flyout_hide();
                }
                return 0;
            }
            break;

        case WM_DESTROY:
            clear_controls();
            return 0;
    }

    return DefWindowProcW(hwnd, uMsg, wParam, lParam);
}

void flyout_init(HINSTANCE hInstance, HWND hDaemonWnd, AppSettings *settings) {
    g_hInstance = hInstance;
    g_hDaemonWnd = hDaemonWnd;
    g_settings = settings;

    INITCOMMONCONTROLSEX icc;
    memset(&icc, 0, sizeof(icc));
    icc.dwSize = sizeof(icc);
    icc.dwICC = ICC_BAR_CLASSES | ICC_STANDARD_CLASSES;
    InitCommonControlsEx(&icc);

    update_theme_colors();

    WNDCLASSEXW wc;
    memset(&wc, 0, sizeof(wc));
    wc.cbSize = sizeof(WNDCLASSEXW);
    wc.lpfnWndProc = FlyoutWndProc;
    wc.hInstance = hInstance;
    wc.lpszClassName = FLYOUT_CLASS_NAME;
    wc.hbrBackground = g_hBgBrush;
    wc.hCursor = LoadCursorW(NULL, (LPCWSTR)IDC_ARROW);
    RegisterClassExW(&wc);

    WNDCLASSEXW wcs;
    memset(&wcs, 0, sizeof(wcs));
    wcs.cbSize = sizeof(WNDCLASSEXW);
    wcs.lpfnWndProc = SubmenuWndProc;
    wcs.hInstance = hInstance;
    wcs.lpszClassName = SUBMENU_CLASS_NAME;
    wcs.hbrBackground = g_hBgBrush;
    wcs.hCursor = LoadCursorW(NULL, (LPCWSTR)IDC_ARROW);
    RegisterClassExW(&wcs);
}

void flyout_cleanup(void) {
    if (g_hSubmenu) {
        DestroyWindow(g_hSubmenu);
        g_hSubmenu = NULL;
    }
    if (g_hFlyout) {
        DestroyWindow(g_hFlyout);
        g_hFlyout = NULL;
    }
    if (g_hFontNormal) {
        DeleteObject(g_hFontNormal);
        g_hFontNormal = NULL;
    }
    if (g_hFontBold) {
        DeleteObject(g_hFontBold);
        g_hFontBold = NULL;
    }
    if (g_hBgBrush) {
        DeleteObject(g_hBgBrush);
        g_hBgBrush = NULL;
    }
    if (g_hHoverBrush) {
        DeleteObject(g_hHoverBrush);
        g_hHoverBrush = NULL;
    }
    UnregisterClassW(SUBMENU_CLASS_NAME, g_hInstance);
    UnregisterClassW(FLYOUT_CLASS_NAME, g_hInstance);
}

bool flyout_is_visible(void) {
    return g_hFlyout && IsWindowVisible(g_hFlyout);
}

void flyout_hide(void) {
    submenu_hide();
    if (g_hFlyout && IsWindowVisible(g_hFlyout)) {
        ShowWindow(g_hFlyout, SW_HIDE);
    }
    g_mainHovered = -1;
}

void flyout_update_values(void) {
    if (!flyout_is_visible()) return;

    for (int i = 0; i < g_controlCount; i++) {
        MonitorInfo info;
        if (brightness_get_info(g_controls[i].monitor_index, &info)) {
            if (g_controls[i].hTrackbar) {
                SendMessageW(g_controls[i].hTrackbar, TBM_SETPOS, TRUE, info.current_brightness);
            }
            if (g_controls[i].hLabelVal) {
                wchar_t buf[32];
                format_brightness_text(info.current_brightness, buf, sizeof(buf) / sizeof(wchar_t));
                SetWindowTextW(g_controls[i].hLabelVal, buf);
            }
        }
    }
}

typedef struct {
    DWORD cbSize;
    HWND hWnd;
    UINT uID;
    GUID guidItem;
} TRAY_IDENTIFIER;

void flyout_show(HWND hTrayWnd) {
    (void)hTrayWnd;
    submenu_hide();
    update_theme_colors();

    // Locate anchor monitor BEFORE sizing so DPI matches target screen
    bool gotIconRect = false;
    RECT rcIcon;
    memset(&rcIcon, 0, sizeof(rcIcon));
    POINT anchorPt;
    GetCursorPos(&anchorPt);

    HMODULE hShell32 = GetModuleHandleW(L"shell32.dll");
    if (hShell32) {
        typedef HRESULT (WINAPI *Shell_NotifyIconGetRectFn)(const TRAY_IDENTIFIER*, RECT*);
        Shell_NotifyIconGetRectFn fnGetRect = (Shell_NotifyIconGetRectFn)(void *)GetProcAddress(hShell32, "Shell_NotifyIconGetRect");
        if (fnGetRect) {
            TRAY_IDENTIFIER nid;
            memset(&nid, 0, sizeof(nid));
            nid.cbSize = sizeof(nid);
            nid.hWnd = g_hDaemonWnd;
            nid.uID = 1;
            HRESULT hr = fnGetRect(&nid, &rcIcon);
            if (SUCCEEDED(hr) && !IsRectEmpty(&rcIcon) && MonitorFromRect(&rcIcon, MONITOR_DEFAULTTONULL) != NULL) {
                anchorPt.x = (rcIcon.left + rcIcon.right) / 2;
                anchorPt.y = (rcIcon.top + rcIcon.bottom) / 2;
                gotIconRect = true;
            }
        }
    }

    HMONITOR hMon = MonitorFromPoint(anchorPt, MONITOR_DEFAULTTONEAREST);
    MONITORINFO mi;
    memset(&mi, 0, sizeof(mi));
    mi.cbSize = sizeof(mi);
    GetMonitorInfoW(hMon, &mi);

    UINT dpi = get_monitor_dpi(hMon);
    g_currentDpi = dpi;
    create_fonts(dpi);

    int count = brightness_get_count();
    if (count == 0) {
        brightness_refresh_monitors();
        count = brightness_get_count();
    }

    int width = dip(268, dpi);
    int sliderBlockH = dip(52, dpi);
    int curY = dip(8, dpi);

    clear_controls();
    g_mainRowCount = 0;
    g_mainHovered = -1;

    // Reserve Y space for brightness slider blocks at top
    int slidersStartY = curY;
    if (count > 0) {
        curY += count * sliderBlockH;
        add_menu_row(g_mainRows, &g_mainRowCount, 0, NULL, true, false, false, false, width, &curY, dpi);
    }

    bool autoChecked = g_settings ? (g_settings->auto_mode == AUTO_MODE_AUTO) : false;
    bool fsChecked = g_settings ? g_settings->hide_taskbar_when_fullscreen : false;

    add_menu_row(g_mainRows, &g_mainRowCount, IDM_AUTO_MODE, L"Auto Mode",
                 false, autoChecked, false, false, width, &curY, dpi);
    add_menu_row(g_mainRows, &g_mainRowCount, IDM_HIDE_WHEN_FULLSCREEN, L"Hide when Fullscreen",
                 false, fsChecked, false, false, width, &curY, dpi);
    add_menu_row(g_mainRows, &g_mainRowCount, 0, NULL, true, false, false, false, width, &curY, dpi);
    add_menu_row(g_mainRows, &g_mainRowCount, IDM_SCREEN_EFFECTS_SUBMENU, L"Screen Effects",
                 false, false, false, true, width, &curY, dpi);
    add_menu_row(g_mainRows, &g_mainRowCount, 0, NULL, true, false, false, false, width, &curY, dpi);
    add_menu_row(g_mainRows, &g_mainRowCount, IDM_SETTINGS, L"Settings...",
                 false, false, true, false, width, &curY, dpi);
    add_menu_row(g_mainRows, &g_mainRowCount, IDM_EXIT, L"Exit",
                 false, false, false, false, width, &curY, dpi);

    int total_height = curY + dip(6, dpi);

    if (!g_hFlyout) {
        g_hFlyout = CreateWindowExW(
            WS_EX_TOOLWINDOW | WS_EX_TOPMOST,
            FLYOUT_CLASS_NAME,
            L"SmartTaskbar++ Menu",
            WS_POPUP | WS_CLIPCHILDREN,
            CW_USEDEFAULT, CW_USEDEFAULT, width, total_height,
            NULL, NULL, g_hInstance, NULL
        );
        daemon_log("flyout_show: CreateWindowExW g_hFlyout=%p err=%lu", (void*)g_hFlyout, g_hFlyout ? 0 : GetLastError());
        if (!g_hFlyout) return;
    }
    apply_dwm_popup_style(g_hFlyout);

    int gap = dip(8, dpi);
    int margin = dip(8, dpi);
    int posX, posY;
    if (gotIconRect) {
        posX = anchorPt.x - width / 2;
        posY = rcIcon.top - total_height - gap;
        if (posY < mi.rcWork.top + margin) {
            posY = rcIcon.bottom + gap;
        }
    } else {
        posX = anchorPt.x - width / 2;
        posY = anchorPt.y - total_height - gap;
        if (posY > mi.rcWork.bottom - total_height - margin) {
            posY = mi.rcWork.bottom - total_height - margin;
        }
    }

    if (posX + width > mi.rcWork.right - margin) posX = mi.rcWork.right - width - margin;
    if (posX < mi.rcWork.left + margin) posX = mi.rcWork.left + margin;
    if (posY + total_height > mi.rcWork.bottom - margin) posY = mi.rcWork.bottom - total_height - margin;
    if (posY < mi.rcWork.top + margin) posY = mi.rcWork.top + margin;

    SetWindowPos(g_hFlyout, HWND_TOPMOST, posX, posY, width, total_height, SWP_NOACTIVATE);

    // Populate monitor slider controls in reserved header space
    int padX = dip(14, dpi);
    int sliderY = slidersStartY;
    for (int i = 0; i < count && g_controlCount < MAX_APP_MONITORS; i++) {
        MonitorInfo info;
        if (!brightness_get_info(i, &info)) continue;

        int valW = dip(56, dpi);
        int labelW = width - padX * 2 - valW;

        HWND hName = CreateWindowExW(
            0, L"STATIC", info.display_name,
            WS_CHILD | WS_VISIBLE | SS_LEFT | SS_ENDELLIPSIS,
            padX, sliderY, labelW, dip(18, dpi),
            g_hFlyout, NULL, g_hInstance, NULL
        );
        SendMessageW(hName, WM_SETFONT, (WPARAM)g_hFontNormal, TRUE);

        wchar_t valBuf[32];
        format_brightness_text(info.current_brightness, valBuf, sizeof(valBuf) / sizeof(wchar_t));
        HWND hVal = CreateWindowExW(
            0, L"STATIC", valBuf,
            WS_CHILD | WS_VISIBLE | SS_RIGHT,
            padX + labelW, sliderY, valW, dip(18, dpi),
            g_hFlyout, NULL, g_hInstance, NULL
        );
        SendMessageW(hVal, WM_SETFONT, (WPARAM)g_hFontBold, TRUE);

        HWND hTrack = CreateWindowExW(
            0, TRACKBAR_CLASSW, L"",
            WS_CHILD | WS_VISIBLE | WS_TABSTOP | TBS_HORZ | TBS_NOTICKS,
            padX - dip(4, dpi), sliderY + dip(20, dpi), width - padX * 2 + dip(8, dpi), dip(26, dpi),
            g_hFlyout, NULL, g_hInstance, NULL
        );

        SendMessageW(hTrack, TBM_SETRANGE, TRUE, MAKELPARAM(0, 100));
        SendMessageW(hTrack, TBM_SETPOS, TRUE, info.current_brightness);
        SendMessageW(hTrack, TBM_SETPAGESIZE, 0, 5);
        SetWindowLongPtrW(hTrack, GWLP_USERDATA, (LONG_PTR)i);

        g_controls[g_controlCount].hLabelName = hName;
        g_controls[g_controlCount].hLabelVal = hVal;
        g_controls[g_controlCount].hTrackbar = hTrack;
        g_controls[g_controlCount].monitor_index = i;
        g_controlCount++;

        sliderY += sliderBlockH;
    }

    g_lastShowTick = GetTickCount64();
    AllowSetForegroundWindow(ASFW_ANY);
    SetWindowPos(g_hFlyout, HWND_TOPMOST, posX, posY, width, total_height, SWP_SHOWWINDOW);
    BOOL fg = SetForegroundWindow(g_hFlyout);
    InvalidateRect(g_hFlyout, NULL, TRUE);
    UpdateWindow(g_hFlyout);
    daemon_log("flyout_show: unified menu at (%d,%d,%d,%d) monitors=%d rows=%d fg=%d dpi=%u",
               posX, posY, width, total_height, count, g_mainRowCount, fg, dpi);
}

void flyout_toggle(HWND hTrayWnd) {
    if (flyout_is_visible()) {
        flyout_hide();
    } else {
        flyout_show(hTrayWnd);
    }
}
