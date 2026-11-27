#pragma once

#include <windows.h>
#include <stdbool.h>

void hotkeys_init(HWND hwnd);
void hotkeys_cleanup(void);
void hotkeys_reload(void);
void hotkeys_handle_message(WPARAM wParam, LPARAM lParam);
