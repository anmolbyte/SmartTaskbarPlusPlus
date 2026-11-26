#pragma once

#include <windows.h>
#include <stdbool.h>
#include "settings.h"

void tray_init(HWND hwnd, HINSTANCE hInstance);
void tray_cleanup(void);
void tray_update_icon(void);
UINT tray_get_taskbar_restart_msg(void);
void tray_on_message(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam, AppSettings *settings);
void tray_launch_settings(HWND hwnd);
