#pragma once

#include <windows.h>
#include <stdbool.h>
#include "settings.h"

typedef enum {
    TASKBAR_POS_BOTTOM = 0,
    TASKBAR_POS_TOP = 1,
    TASKBAR_POS_LEFT = 2,
    TASKBAR_POS_RIGHT = 3
} TaskbarPosition;

typedef struct {
    HWND handle;
    RECT rect;
    bool is_show;
    TaskbarPosition position;
    HMONITOR monitor;
} TaskbarInfo;

typedef enum {
    TASKBAR_BEHAVIOR_DO_NOTHING = 0,
    TASKBAR_BEHAVIOR_PENDING = 1,
    TASKBAR_BEHAVIOR_SHOW = 2,
    TASKBAR_BEHAVIOR_HIDE = 3
} TaskbarBehavior;

void taskbar_engine_init(HWND hwnd_main);
void taskbar_engine_tick(const AppSettings *settings);
void taskbar_engine_stop(const AppSettings *settings);
void taskbar_engine_restore(bool show_taskbar);
void taskbar_engine_invalidate_cache(void);
