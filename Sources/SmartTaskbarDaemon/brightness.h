#pragma once

#include <windows.h>
#include <stdbool.h>

#define MAX_APP_MONITORS 16

typedef struct {
    char hardware_id[128];
    char legacy_id[128];
    wchar_t device_name[32];      // e.g. \\.\DISPLAY1
    wchar_t model_name[64];       // e.g. DELL U2718Q
    wchar_t display_name[64];     // Nickname or model_name
    bool is_internal;             // true if laptop internal panel
    bool has_physical_handle;     // true if physical_handle was acquired (0 can be a valid DXVA2 handle!)
    HANDLE physical_handle;       // physical monitor handle
    int current_brightness;       // 0 to 100
    int min_brightness;
    int max_brightness;
    int physical_index;
    wchar_t wmi_instance_name[256];
} MonitorInfo;

void brightness_init(HWND main_hwnd);
void brightness_cleanup(void);
void brightness_refresh_monitors(void);
int brightness_get_count(void);
bool brightness_get_info(int index, MonitorInfo *out_info);
void brightness_set_direct(int index, int brightness);
void brightness_adjust(const char *target_id, int action, int value);
