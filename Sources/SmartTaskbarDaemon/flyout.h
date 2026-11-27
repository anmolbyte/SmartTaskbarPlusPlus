#pragma once

#include <windows.h>
#include <stdbool.h>
#include "settings.h"

void flyout_init(HINSTANCE hInstance, HWND hDaemonWnd, AppSettings *settings);
void flyout_cleanup(void);
void flyout_show(HWND hTrayWnd);
void flyout_hide(void);
void flyout_toggle(HWND hTrayWnd);
void flyout_update_values(void);
bool flyout_is_visible(void);
