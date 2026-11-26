#pragma once

#include <windows.h>
#include <stdbool.h>
#include <stddef.h>

#define SETTINGS_FILE_MUTEX_NAME    L"SmartTaskbar_Settings_File_Mutex"
#define SETTINGS_RELOAD_EVENT_NAME  L"SmartTaskbar_ReloadSettings"
#define SETTINGS_UI_RELOAD_EVENT_NAME L"SmartTaskbar_UIReloadSettings"
#define DAEMON_MUTEX_NAME           L"SmartTaskbar_Daemon_Mutex"
#define SETTINGS_APP_MUTEX_NAME     L"SmartTaskbar_Settings_Mutex"

typedef enum {
    AUTO_MODE_NONE = 0,
    AUTO_MODE_AUTO = 1
} AutoModeType;

typedef enum {
    CLICK_ACTION_TOGGLE_INVERSION = 0,
    CLICK_ACTION_TOGGLE_AUTO_MODE = 1,
    CLICK_ACTION_SET_SHOW_MODE = 2,
    CLICK_ACTION_SET_HIDE_MODE = 3,
    CLICK_ACTION_NONE = 4
} TrayClickAction;

typedef enum {
    SCREEN_DETECTION_PRIMARY = 0,
    SCREEN_DETECTION_ANY = 1
} LargeScreenDetectionMode;

typedef struct {
    AutoModeType auto_mode;
    bool show_taskbar_when_exit;
    bool hide_taskbar_when_fullscreen;
    bool start_on_login;
    double large_screen_threshold;
    bool disable_large_screen_override;
    bool is_negative_enabled;
    char active_color_effect[64];
    TrayClickAction click_action;
    LargeScreenDetectionMode detection_mode;
} AppSettings;

void settings_get_default(AppSettings *settings);
bool settings_get_path(wchar_t *out_path, size_t max_len);
bool settings_load(AppSettings *settings);
bool settings_save(const AppSettings *settings);
void settings_signal_reload(void);
void settings_signal_ui_reload(void);
void settings_signal_daemon_reload(void);
