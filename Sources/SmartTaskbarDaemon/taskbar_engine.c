#include "taskbar_engine.h"
#include <math.h>
#include <string.h>
#include <stdlib.h>
#include <dwmapi.h>

#define TRAY_BAR_FLAG           0x05D1
#define TRAY_TOLERANCE          6
#define MAX_HANDLES             128
#define MAX_STACK               64

typedef HRESULT (WINAPI *pfnDwmGetWindowAttribute)(HWND, DWORD, PVOID, DWORD);
static pfnDwmGetWindowAttribute g_pfnDwmGetWindowAttribute = NULL;
static HMODULE g_hDwmapi = NULL;

typedef struct {
    HWND items[MAX_HANDLES];
    int count;
} HandleSet;

static void set_clear(HandleSet *set) {
    set->count = 0;
}

static bool set_contains(const HandleSet *set, HWND hwnd) {
    for (int i = 0; i < set->count; i++) {
        if (set->items[i] == hwnd) return true;
    }
    return false;
}

static void set_add(HandleSet *set, HWND hwnd) {
    if (set_contains(set, hwnd)) return;
    if (set->count < MAX_HANDLES) {
        set->items[set->count++] = hwnd;
    } else {
        memmove(&set->items[0], &set->items[1], sizeof(HWND) * (MAX_HANDLES - 1));
        set->items[MAX_HANDLES - 1] = hwnd;
    }
}

typedef struct {
    HWND items[MAX_STACK];
    int count;
} HandleStack;

static void stack_clear(HandleStack *st) {
    st->count = 0;
}

static bool stack_contains(const HandleStack *st, HWND hwnd) {
    for (int i = 0; i < st->count; i++) {
        if (st->items[i] == hwnd) return true;
    }
    return false;
}

static void stack_push(HandleStack *st, HWND hwnd) {
    if (stack_contains(st, hwnd)) return;
    if (st->count < MAX_STACK) {
        st->items[st->count++] = hwnd;
    }
}

static HWND stack_peek(const HandleStack *st) {
    if (st->count > 0) return st->items[st->count - 1];
    return NULL;
}

static HWND stack_pop(HandleStack *st) {
    if (st->count > 0) return st->items[--st->count];
    return NULL;
}

// Engine State
static int g_timerCount = 0;
static TaskbarInfo g_taskbar;
static HWND g_currentForegroundWindow = NULL;

static HandleSet g_nonMouseOverShowHandleSet;
static HandleSet g_nonDesktopShowHandleSet;
static HandleSet g_nonForegroundShowHandleSet;
static HandleSet g_desktopHandleSet;
static HandleStack g_lastHideForegroundHandle;

static double g_cachedPrimaryDiagonal = 0.0;
static double g_cachedMaxDiagonal = 0.0;

void taskbar_engine_invalidate_cache(void) {
    g_cachedPrimaryDiagonal = 0.0;
    g_cachedMaxDiagonal = 0.0;
    set_clear(&g_nonMouseOverShowHandleSet);
    set_clear(&g_nonDesktopShowHandleSet);
    set_clear(&g_nonForegroundShowHandleSet);
    set_clear(&g_desktopHandleSet);
    stack_clear(&g_lastHideForegroundHandle);
}

void taskbar_engine_init(HWND hwnd_main) {
    (void)hwnd_main;
    memset(&g_taskbar, 0, sizeof(g_taskbar));
    taskbar_engine_invalidate_cache();

    if (!g_hDwmapi) {
        g_hDwmapi = LoadLibraryW(L"dwmapi.dll");
        if (g_hDwmapi) {
            FARPROC proc = GetProcAddress(g_hDwmapi, "DwmGetWindowAttribute");
            memcpy(&g_pfnDwmGetWindowAttribute, &proc, sizeof(proc));
        }
    }
}

static HWND get_main_taskbar_handle(void) {
    return FindWindowW(L"Shell_TrayWnd", NULL);
}

static HMONITOR get_main_taskbar_monitor(void) {
    HWND h = get_main_taskbar_handle();
    return h ? MonitorFromWindow(h, MONITOR_DEFAULTTONEAREST) : NULL;
}

static bool is_not_autohide(HWND hTaskbar) {
    APPBARDATA abd;
    memset(&abd, 0, sizeof(APPBARDATA));
    abd.cbSize = sizeof(APPBARDATA);
    abd.hWnd = hTaskbar;
    UINT state = (UINT)SHAppBarMessage(ABM_GETSTATE, &abd);
    return (state & ABS_AUTOHIDE) == 0;
}

static void set_autohide(HWND hTaskbar) {
    if (!is_not_autohide(hTaskbar)) return;
    APPBARDATA abd;
    memset(&abd, 0, sizeof(APPBARDATA));
    abd.cbSize = sizeof(APPBARDATA);
    abd.hWnd = hTaskbar;
    abd.lParam = ABS_AUTOHIDE;
    SHAppBarMessage(ABM_SETSTATE, &abd);
}

static void cancel_autohide(HWND hTaskbar) {
    if (is_not_autohide(hTaskbar)) return;
    APPBARDATA abd;
    memset(&abd, 0, sizeof(APPBARDATA));
    abd.cbSize = sizeof(APPBARDATA);
    abd.hWnd = hTaskbar;
    abd.lParam = ABS_ALWAYSONTOP;
    SHAppBarMessage(ABM_SETSTATE, &abd);
}

static void post_show_taskbar(const TaskbarInfo *tb) {
    if (!tb->is_show && tb->handle) {
        PostMessageW(tb->handle, TRAY_BAR_FLAG, (WPARAM)1, (LPARAM)tb->monitor);
    }
}

static void post_hide_taskbar(const TaskbarInfo *tb) {
    if (tb->is_show && tb->handle) {
        PostMessageW(tb->handle, TRAY_BAR_FLAG, 0, 0);
    }
}

static bool init_taskbar_info(TaskbarInfo *out_tb) {
    memset(out_tb, 0, sizeof(TaskbarInfo));
    HWND handle = get_main_taskbar_handle();
    if (!handle) return false;

    RECT rect;
    if (!GetWindowRect(handle, &rect)) return false;

    HMONITOR mon = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);
    if (!mon) return false;

    MONITORINFO mi;
    memset(&mi, 0, sizeof(MONITORINFO));
    mi.cbSize = sizeof(MONITORINFO);
    if (!GetMonitorInfoW(mon, &mi)) return false;

    int monWidth = mi.rcMonitor.right - mi.rcMonitor.left;

    out_tb->handle = handle;
    out_tb->monitor = mon;

    if (abs((rect.right - rect.left) - monWidth) <= 2) {
        int bottomDelta = rect.bottom - mi.rcMonitor.bottom;
        if (bottomDelta == 0) {
            out_tb->rect = rect;
            out_tb->is_show = true;
            out_tb->position = TASKBAR_POS_BOTTOM;
            return true;
        }
        if (bottomDelta > 0) {
            out_tb->rect.left = rect.left;
            out_tb->rect.top = rect.top - bottomDelta;
            out_tb->rect.right = rect.right;
            out_tb->rect.bottom = rect.bottom - bottomDelta;
            out_tb->is_show = false;
            out_tb->position = TASKBAR_POS_BOTTOM;
            return true;
        }

        int topDelta = rect.top - mi.rcMonitor.top;
        out_tb->rect.left = rect.left;
        out_tb->rect.top = rect.top - topDelta;
        out_tb->rect.right = rect.right;
        out_tb->rect.bottom = rect.bottom - topDelta;
        out_tb->is_show = (topDelta == 0);
        out_tb->position = TASKBAR_POS_TOP;
        return true;
    }

    int leftDelta = rect.left - mi.rcMonitor.left;
    if (leftDelta == 0) {
        out_tb->rect = rect;
        out_tb->is_show = true;
        out_tb->position = TASKBAR_POS_LEFT;
        return true;
    }
    if (leftDelta < 0) {
        out_tb->rect.left = rect.left - leftDelta;
        out_tb->rect.top = rect.top;
        out_tb->rect.right = rect.right - leftDelta;
        out_tb->rect.bottom = rect.bottom;
        out_tb->is_show = false;
        out_tb->position = TASKBAR_POS_LEFT;
        return true;
    }

    int rightDelta = rect.right - mi.rcMonitor.right;
    out_tb->rect.left = rect.left - rightDelta;
    out_tb->rect.top = rect.top;
    out_tb->rect.right = rect.right - rightDelta;
    out_tb->rect.bottom = rect.bottom;
    out_tb->is_show = (rightDelta == 0);
    out_tb->position = TASKBAR_POS_RIGHT;
    return true;
}

static double get_primary_display_diagonal_inches(void) {
    if (g_cachedPrimaryDiagonal > 0.0) return g_cachedPrimaryDiagonal;
    HDC hdc = GetDC(NULL);
    if (!hdc) return 0.0;
    int wMm = GetDeviceCaps(hdc, HORZSIZE);
    int hMm = GetDeviceCaps(hdc, VERTSIZE);
    ReleaseDC(NULL, hdc);

    double diagMm = sqrt((double)(wMm * wMm + hMm * hMm));
    g_cachedPrimaryDiagonal = diagMm / 25.4;
    return g_cachedPrimaryDiagonal;
}

static BOOL CALLBACK enum_monitors_diag(HMONITOR hMon, HDC hdcMon, LPRECT lprc, LPARAM dwData) {
    (void)hdcMon; (void)lprc;
    double *pMax = (double *)dwData;
    MONITORINFOEXW mi;
    memset(&mi, 0, sizeof(MONITORINFOEXW));
    mi.cbSize = sizeof(MONITORINFOEXW);
    if (GetMonitorInfoW(hMon, (LPMONITORINFO)&mi)) {
        HDC hdc = CreateDCW(L"DISPLAY", mi.szDevice, NULL, NULL);
        if (hdc) {
            int wMm = GetDeviceCaps(hdc, HORZSIZE);
            int hMm = GetDeviceCaps(hdc, VERTSIZE);
            double diagInches = sqrt((double)(wMm * wMm + hMm * hMm)) / 25.4;
            if (diagInches > *pMax) *pMax = diagInches;
            DeleteDC(hdc);
        }
    }
    return TRUE;
}

static double get_max_display_diagonal_inches(void) {
    if (g_cachedMaxDiagonal > 0.0) return g_cachedMaxDiagonal;
    double maxDiag = 0.0;
    EnumDisplayMonitors(NULL, NULL, enum_monitors_diag, (LPARAM)&maxDiag);
    g_cachedMaxDiagonal = maxDiag;
    return g_cachedMaxDiagonal;
}

static bool is_foreground_fullscreen(HMONITOR taskbarMonitor) {
    HWND fg = GetForegroundWindow();
    if (!fg) return false;

    wchar_t cls[128];
    if (GetClassNameW(fg, cls, 128) > 0) {
        if (wcscmp(cls, L"Progman") == 0 ||
            wcscmp(cls, L"WorkerW") == 0 ||
            wcscmp(cls, L"Windows.UI.Core.CoreWindow") == 0 ||
            wcscmp(cls, L"TaskListThumbnailWnd") == 0 ||
            wcscmp(cls, L"XamlExplorerHostIslandWindow") == 0 ||
            wcscmp(cls, L"Windows.UI.Composition.DesktopWindowContentBridge") == 0 ||
            wcscmp(cls, L"MultitaskingViewFrame") == 0 ||
            wcscmp(cls, L"TaskSwitcherWnd") == 0 ||
            wcscmp(cls, L"#32771") == 0) {
            return false;
        }
    }

    HMONITOR mon = MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST);
    if (mon != taskbarMonitor) return false;

    WINDOWPLACEMENT wp;
    memset(&wp, 0, sizeof(WINDOWPLACEMENT));
    wp.length = sizeof(WINDOWPLACEMENT);
    if (GetWindowPlacement(fg, &wp)) {
        if (wp.showCmd == SW_SHOWMAXIMIZED) return false;
    }

    LONG_PTR style = GetWindowLongPtrW(fg, GWL_STYLE);
    if ((style & WS_CAPTION) == WS_CAPTION) return false;

    RECT rect;
    RECT frameRect;
    memset(&frameRect, 0, sizeof(RECT));
    bool have_frame = false;
    if (g_pfnDwmGetWindowAttribute) {
        if (SUCCEEDED(g_pfnDwmGetWindowAttribute(fg, DWMWA_EXTENDED_FRAME_BOUNDS, &frameRect, sizeof(RECT)))) {
            if (frameRect.right > frameRect.left && frameRect.bottom > frameRect.top) {
                have_frame = true;
            }
        }
    }

    if (have_frame) {
        rect = frameRect;
    } else {
        if (!GetWindowRect(fg, &rect)) return false;
    }

    MONITORINFO mi;
    memset(&mi, 0, sizeof(MONITORINFO));
    mi.cbSize = sizeof(MONITORINFO);
    if (!GetMonitorInfoW(mon, &mi)) return false;

    if (rect.left <= mi.rcMonitor.left &&
        rect.top <= mi.rcMonitor.top &&
        rect.right >= mi.rcMonitor.right &&
        rect.bottom >= mi.rcMonitor.bottom) {
        return true;
    }

    return false;
}

static TaskbarBehavior check_if_mouse_over(const TaskbarInfo *tb, const AppSettings *settings) {
    if (settings->hide_taskbar_when_fullscreen && is_foreground_fullscreen(tb->monitor)) {
        return TASKBAR_BEHAVIOR_PENDING;
    }

    POINT pt;
    if (!GetCursorPos(&pt)) return TASKBAR_BEHAVIOR_PENDING;

    HWND mouseOver = WindowFromPoint(pt);
    if (!mouseOver) return TASKBAR_BEHAVIOR_PENDING;

    if (mouseOver == tb->handle) return TASKBAR_BEHAVIOR_DO_NOTHING;
    if (GetAncestor(mouseOver, GA_ROOT) == tb->handle) return TASKBAR_BEHAVIOR_DO_NOTHING;

    RECT moRect;
    if (GetWindowRect(mouseOver, &moRect)) {
        if (moRect.top >= tb->rect.top - TRAY_TOLERANCE &&
            moRect.bottom <= tb->rect.bottom + TRAY_TOLERANCE &&
            moRect.left >= tb->rect.left - TRAY_TOLERANCE &&
            moRect.right <= tb->rect.right + TRAY_TOLERANCE) {
            wchar_t cls[64];
            GetClassNameW(mouseOver, cls, 64);
            if (wcscmp(cls, L"Progman") != 0 && wcscmp(cls, L"WorkerW") != 0) {
                return TASKBAR_BEHAVIOR_DO_NOTHING;
            }
        }
    }

    if (set_contains(&g_nonMouseOverShowHandleSet, mouseOver)) {
        return TASKBAR_BEHAVIOR_PENDING;
    }

    wchar_t cls[64];
    GetClassNameW(mouseOver, cls, 64);
    if (wcscmp(cls, L"TaskListThumbnailWnd") == 0) {
        return TASKBAR_BEHAVIOR_SHOW;
    }

    set_add(&g_nonMouseOverShowHandleSet, mouseOver);
    return TASKBAR_BEHAVIOR_PENDING;
}

static bool check_if_window_should_hide_taskbar(const TaskbarInfo *tb, HWND fg) {
    if (!fg || fg == tb->handle || !IsWindowVisible(fg) || IsIconic(fg)) return false;

    if (MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST) != tb->monitor) return false;

    RECT rect;
    if (!GetWindowRect(fg, &rect)) return false;

    if (rect.bottom <= tb->rect.top ||
        rect.top >= tb->rect.bottom ||
        rect.left >= tb->rect.right ||
        rect.right <= tb->rect.left) {
        return false;
    }

    DWORD pid = 0;
    GetWindowThreadProcessId(fg, &pid);
    if (pid == 0) return false;

    return true;
}

static HWND get_taskbar_sample_window(const TaskbarInfo *tb) {
    POINT pt;
    switch (tb->position) {
        case TASKBAR_POS_BOTTOM:
            pt.x = tb->rect.left + (tb->rect.right - tb->rect.left) / 2;
            pt.y = tb->rect.top;
            break;
        case TASKBAR_POS_LEFT:
            pt.x = tb->rect.right;
            pt.y = tb->rect.top + (tb->rect.bottom - tb->rect.top) / 2;
            break;
        case TASKBAR_POS_RIGHT:
            pt.x = tb->rect.left;
            pt.y = tb->rect.top + (tb->rect.bottom - tb->rect.top) / 2;
            break;
        case TASKBAR_POS_TOP:
            pt.x = tb->rect.left + (tb->rect.right - tb->rect.left) / 2;
            pt.y = tb->rect.bottom;
            break;
        default:
            pt.x = tb->rect.left + (tb->rect.right - tb->rect.left) / 2;
            pt.y = tb->rect.top;
            break;
    }
    return WindowFromPoint(pt);
}

static bool check_if_desktop_show(const TaskbarInfo *tb) {
    HWND w = get_taskbar_sample_window(tb);
    if (!w || w == tb->handle) return false;

    HWND root = GetAncestor(w, GA_ROOT);
    if (!root || root == tb->handle) return false;

    if (set_contains(&g_desktopHandleSet, root)) return true;
    if (set_contains(&g_nonDesktopShowHandleSet, root)) return false;

    wchar_t cls[64];
    GetClassNameW(root, cls, 64);
    if (wcscmp(cls, L"Progman") == 0 || wcscmp(cls, L"WorkerW") == 0) {
        set_add(&g_desktopHandleSet, root);
        return true;
    }

    set_add(&g_nonDesktopShowHandleSet, root);
    return false;
}

static bool area_compare(const RECT *winRect, HMONITOR mon) {
    MONITORINFO mi;
    memset(&mi, 0, sizeof(MONITORINFO));
    mi.cbSize = sizeof(MONITORINFO);
    if (mon && GetMonitorInfoW(mon, &mi)) {
        long long monArea = (long long)(mi.rcMonitor.right - mi.rcMonitor.left) * (mi.rcMonitor.bottom - mi.rcMonitor.top);
        long long winArea = (long long)(winRect->bottom - winRect->top) * (winRect->right - winRect->left);
        return (8 * winArea > monArea) || (winArea > 25000);
    }
    return true;
}

static void before_show_bar(const TaskbarInfo *tb) {
    while (g_lastHideForegroundHandle.count > 0) {
        HWND top = stack_peek(&g_lastHideForegroundHandle);
        if (!IsWindow(top)) {
            stack_pop(&g_lastHideForegroundHandle);
            continue;
        }

        if (check_if_window_should_hide_taskbar(tb, top)) {
            return;
        }

        stack_pop(&g_lastHideForegroundHandle);
    }

    post_show_taskbar(tb);
}

static void check_current_window(const TaskbarInfo *tb) {
    HWND fg = GetForegroundWindow();
    if (!fg) return;

    if (fg == tb->handle || set_contains(&g_desktopHandleSet, fg)) {
        before_show_bar(tb);
        g_currentForegroundWindow = fg;
        return;
    }

    if (!IsWindowVisible(fg) || IsIconic(fg)) {
        return;
    }

    HMONITOR mon = MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST);
    if (mon != tb->monitor) {
        return;
    }

    RECT fgRect;
    if (!GetWindowRect(fg, &fgRect)) {
        return;
    }

    // Check intersection with taskbar rect
    if (fgRect.bottom <= tb->rect.top ||
        fgRect.top >= tb->rect.bottom ||
        fgRect.left >= tb->rect.right ||
        fgRect.right <= tb->rect.left) {
        before_show_bar(tb);
        g_currentForegroundWindow = fg;
        return;
    }

    DWORD pid = 0;
    GetWindowThreadProcessId(fg, &pid);
    if (pid == 0) return;

    if (set_contains(&g_nonForegroundShowHandleSet, fg)) {
        if (fg != g_currentForegroundWindow) {
            if (!stack_contains(&g_lastHideForegroundHandle, fg) && area_compare(&fgRect, mon)) {
                stack_push(&g_lastHideForegroundHandle, fg);
            }
            post_hide_taskbar(tb);
        }
        g_currentForegroundWindow = fg;
        return;
    }

    wchar_t cls[128];
    GetClassNameW(fg, cls, 128);

    if (wcscmp(cls, L"Progman") == 0 || wcscmp(cls, L"WorkerW") == 0) {
        set_add(&g_desktopHandleSet, fg);
        before_show_bar(tb);
        g_currentForegroundWindow = fg;
        return;
    }

    if (wcscmp(cls, L"Windows.UI.Core.CoreWindow") == 0 ||
        wcscmp(cls, L"XamlExplorerHostIslandWindow") == 0 ||
        wcscmp(cls, L"Windows.UI.Composition.DesktopWindowContentBridge") == 0 ||
        wcscmp(cls, L"StartMenuExperienceHost") == 0 ||
        wcscmp(cls, L"MultitaskingViewFrame") == 0 ||
        wcscmp(cls, L"TaskSwitcherWnd") == 0 ||
        wcscmp(cls, L"#32771") == 0) {
        return;
    }

    set_add(&g_nonForegroundShowHandleSet, fg);
    if (fg != g_currentForegroundWindow) {
        if (!stack_contains(&g_lastHideForegroundHandle, fg) && area_compare(&fgRect, mon)) {
            stack_push(&g_lastHideForegroundHandle, fg);
        }
        post_hide_taskbar(tb);
    }
    g_currentForegroundWindow = fg;
}

void taskbar_engine_tick(const AppSettings *settings) {
    if (settings->auto_mode != AUTO_MODE_AUTO) {
        return;
    }

    // Every 5 ticks (625 ms): re-evaluate environment
    if (g_timerCount % 5 == 0) {
        double currentDiag = (settings->detection_mode == SCREEN_DETECTION_ANY) ?
            get_max_display_diagonal_inches() : get_primary_display_diagonal_inches();

        if (!settings->disable_large_screen_override && currentDiag > settings->large_screen_threshold) {
            HWND h = get_main_taskbar_handle();
            if (h) cancel_autohide(h);
            memset(&g_taskbar, 0, sizeof(TaskbarInfo));
        } else {
            HMONITOR mainMon = get_main_taskbar_monitor();
            if (settings->hide_taskbar_when_fullscreen && is_foreground_fullscreen(mainMon)) {
                HWND h = get_main_taskbar_handle();
                if (h) cancel_autohide(h);
                memset(&g_taskbar, 0, sizeof(TaskbarInfo));
            } else {
                HWND h = get_main_taskbar_handle();
                if (h && is_not_autohide(h)) {
                    set_autohide(h);
                }
                init_taskbar_info(&g_taskbar);
            }
        }
    }

    if (g_taskbar.handle) {
        TaskbarBehavior behavior = check_if_mouse_over(&g_taskbar, settings);
        switch (behavior) {
            case TASKBAR_BEHAVIOR_DO_NOTHING:
                break;
            case TASKBAR_BEHAVIOR_PENDING:
                check_current_window(&g_taskbar);
                if (check_if_desktop_show(&g_taskbar)) {
                    before_show_bar(&g_taskbar);
                }
                break;
            case TASKBAR_BEHAVIOR_SHOW:
                post_show_taskbar(&g_taskbar);
                break;
            default:
                break;
        }
    }

    g_timerCount++;

    // Clear caches every 7200 ticks (15 mins)
    if (g_timerCount > 7200) {
        g_timerCount = 0;
        set_clear(&g_desktopHandleSet);
        set_clear(&g_nonMouseOverShowHandleSet);
        set_clear(&g_nonDesktopShowHandleSet);
        set_clear(&g_nonForegroundShowHandleSet);
    }
}

void taskbar_engine_restore(bool show_taskbar) {
    HWND h = get_main_taskbar_handle();
    if (h) {
        if (show_taskbar) {
            cancel_autohide(h);
            HMONITOR mon = get_main_taskbar_monitor();
            PostMessageW(h, TRAY_BAR_FLAG, (WPARAM)1, (LPARAM)mon);
        } else {
            if (is_not_autohide(h)) {
                set_autohide(h);
            }
            PostMessageW(h, TRAY_BAR_FLAG, 0, 0);
        }
    }
}

void taskbar_engine_stop(const AppSettings *settings) {
    taskbar_engine_restore(settings ? settings->show_taskbar_when_exit : true);
    if (g_hDwmapi) {
        FreeLibrary(g_hDwmapi);
        g_hDwmapi = NULL;
        g_pfnDwmGetWindowAttribute = NULL;
    }
}
