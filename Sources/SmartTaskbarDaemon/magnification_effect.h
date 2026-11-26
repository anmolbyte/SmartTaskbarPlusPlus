#pragma once

#include <windows.h>
#include <stdbool.h>

typedef struct {
    float transform[5][5];
} MAGCOLOREFFECT;

bool mag_init(void);
void mag_uninit(void);
bool mag_apply_effect(const char *effect_name);
bool mag_restore_default(void);
void mag_get_matrix_by_name(const char *name, MAGCOLOREFFECT *out_effect);
