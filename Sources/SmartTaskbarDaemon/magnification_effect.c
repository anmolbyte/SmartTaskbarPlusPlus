#include "magnification_effect.h"
#include <string.h>

typedef BOOL (WINAPI *pfnMagInitialize)(void);
typedef BOOL (WINAPI *pfnMagUninitialize)(void);
typedef BOOL (WINAPI *pfnMagSetFullscreenColorEffect)(MAGCOLOREFFECT *pEffect);

static HMODULE g_hMagDll = NULL;
static pfnMagInitialize g_pfnMagInit = NULL;
static pfnMagUninitialize g_pfnMagUninit = NULL;
static pfnMagSetFullscreenColorEffect g_pfnMagSetColorEffect = NULL;
static bool g_isInitialized = false;

static void matrix_multiply(const float a[5][5], const float b[5][5], float out[5][5]) {
    for (int i = 0; i < 5; i++) {
        for (int j = 0; j < 5; j++) {
            float sum = 0.0f;
            for (int k = 0; k < 5; k++) {
                sum += a[i][k] * b[k][j];
            }
            out[i][j] = sum;
        }
    }
}

static const float MAT_IDENTITY[5][5] = {
    { 1.0f, 0.0f, 0.0f, 0.0f, 0.0f },
    { 0.0f, 1.0f, 0.0f, 0.0f, 0.0f },
    { 0.0f, 0.0f, 1.0f, 0.0f, 0.0f },
    { 0.0f, 0.0f, 0.0f, 1.0f, 0.0f },
    { 0.0f, 0.0f, 0.0f, 0.0f, 1.0f }
};

static const float MAT_NEGATIVE[5][5] = {
    { -1.0f,  0.0f,  0.0f, 0.0f, 0.0f },
    {  0.0f, -1.0f,  0.0f, 0.0f, 0.0f },
    {  0.0f,  0.0f, -1.0f, 0.0f, 0.0f },
    {  0.0f,  0.0f,  0.0f, 1.0f, 0.0f },
    {  1.0f,  1.0f,  1.0f, 0.0f, 1.0f }
};

static const float MAT_GRAYSCALE[5][5] = {
    { 0.3f, 0.3f, 0.3f, 0.0f, 0.0f },
    { 0.6f, 0.6f, 0.6f, 0.0f, 0.0f },
    { 0.1f, 0.1f, 0.1f, 0.0f, 0.0f },
    { 0.0f, 0.0f, 0.0f, 1.0f, 0.0f },
    { 0.0f, 0.0f, 0.0f, 0.0f, 1.0f }
};

static const float MAT_SEPIA[5][5] = {
    { 0.393f, 0.349f, 0.272f, 0.0f, 0.0f },
    { 0.769f, 0.686f, 0.534f, 0.0f, 0.0f },
    { 0.189f, 0.168f, 0.131f, 0.0f, 0.0f },
    { 0.0f,   0.0f,   0.0f,   1.0f, 0.0f },
    { 0.0f,   0.0f,   0.0f,   0.0f, 1.0f }
};

static const float MAT_RED_BASE[5][5] = {
    { 1.0f, 0.0f, 0.0f, 0.0f, 0.0f },
    { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f },
    { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f },
    { 0.0f, 0.0f, 0.0f, 1.0f, 0.0f },
    { 0.0f, 0.0f, 0.0f, 0.0f, 1.0f }
};

static const float MAT_HUESHIFT180[5][5] = {
    { -0.3333333f,  0.6666667f,  0.6666667f, 0.0f, 0.0f },
    {  0.6666667f, -0.3333333f,  0.6666667f, 0.0f, 0.0f },
    {  0.6666667f,  0.6666667f, -0.3333333f, 0.0f, 0.0f },
    {  0.0f,        0.0f,        0.0f,        1.0f, 0.0f },
    {  0.0f,        0.0f,        0.0f,        0.0f, 1.0f }
};

static const float MAT_SMART_1[5][5] = {
    {  1.0f, -1.0f, -1.0f, 0.0f, 0.0f },
    { -1.0f,  1.0f, -1.0f, 0.0f, 0.0f },
    { -1.0f, -1.0f,  1.0f, 0.0f, 0.0f },
    {  0.0f,  0.0f,  0.0f, 1.0f, 0.0f },
    {  1.0f,  1.0f,  1.0f, 0.0f, 1.0f }
};

static const float MAT_SMART_2[5][5] = {
    {  0.39f, -0.62f, -0.62f, 0.0f, 0.0f },
    { -1.21f, -0.22f, -1.22f, 0.0f, 0.0f },
    { -0.16f, -0.16f,  0.84f, 0.0f, 0.0f },
    {   0.0f,   0.0f,   0.0f, 1.0f, 0.0f },
    {   1.0f,   1.0f,   1.0f, 0.0f, 1.0f }
};

static const float MAT_SMART_3[5][5] = {
    {     1.089508f,   -0.9326327f, -0.932633042f, 0.0f, 0.0f },
    {  -1.81771779f,    0.1683074f,  -1.84169245f, 0.0f, 0.0f },
    { -0.244589478f, -0.247815639f,    1.7621845f, 0.0f, 0.0f },
    {          0.0f,          0.0f,          0.0f, 1.0f, 0.0f },
    {          1.0f,          1.0f,          1.0f, 0.0f, 1.0f }
};

static const float MAT_SMART_4[5][5] = {
    {  0.50f, -0.78f, -0.78f, 0.0f, 0.0f },
    { -0.56f,  0.72f, -0.56f, 0.0f, 0.0f },
    { -0.94f, -0.94f,  0.34f, 0.0f, 0.0f },
    {   0.0f,   0.0f,   0.0f, 1.0f, 0.0f },
    {   1.0f,   1.0f,   1.0f, 0.0f, 1.0f }
};

bool mag_init(void) {
    if (g_isInitialized) return true;

    if (!g_hMagDll) {
        g_hMagDll = LoadLibraryW(L"magnification.dll");
        if (!g_hMagDll) return false;
    }

    FARPROC pInit = GetProcAddress(g_hMagDll, "MagInitialize");
    FARPROC pUninit = GetProcAddress(g_hMagDll, "MagUninitialize");
    FARPROC pSetEffect = GetProcAddress(g_hMagDll, "MagSetFullscreenColorEffect");

    memcpy(&g_pfnMagInit, &pInit, sizeof(pInit));
    memcpy(&g_pfnMagUninit, &pUninit, sizeof(pUninit));
    memcpy(&g_pfnMagSetColorEffect, &pSetEffect, sizeof(pSetEffect));

    if (g_pfnMagInit && g_pfnMagSetColorEffect) {
        g_isInitialized = g_pfnMagInit();
        return g_isInitialized;
    }
    return false;
}

void mag_uninit(void) {
    if (g_isInitialized) {
        mag_restore_default();
        if (g_pfnMagUninit) {
            g_pfnMagUninit();
        }
        g_isInitialized = false;
    }
    if (g_hMagDll) {
        FreeLibrary(g_hMagDll);
        g_hMagDll = NULL;
    }
}

void mag_get_matrix_by_name(const char *name, MAGCOLOREFFECT *out_effect) {
    if (!out_effect) return;

    if (!name || strcmp(name, "Identity") == 0) {
        memcpy(out_effect->transform, MAT_IDENTITY, sizeof(MAT_IDENTITY));
    } else if (strcmp(name, "Negative") == 0) {
        memcpy(out_effect->transform, MAT_NEGATIVE, sizeof(MAT_NEGATIVE));
    } else if (strcmp(name, "GrayScale") == 0) {
        memcpy(out_effect->transform, MAT_GRAYSCALE, sizeof(MAT_GRAYSCALE));
    } else if (strcmp(name, "Sepia") == 0) {
        memcpy(out_effect->transform, MAT_SEPIA, sizeof(MAT_SEPIA));
    } else if (strcmp(name, "Red") == 0) {
        matrix_multiply(MAT_GRAYSCALE, MAT_RED_BASE, out_effect->transform);
    } else if (strcmp(name, "HueShift180") == 0) {
        memcpy(out_effect->transform, MAT_HUESHIFT180, sizeof(MAT_HUESHIFT180));
    } else if (strcmp(name, "NegativeGrayScale") == 0) {
        matrix_multiply(MAT_NEGATIVE, MAT_GRAYSCALE, out_effect->transform);
    } else if (strcmp(name, "NegativeSepia") == 0) {
        matrix_multiply(MAT_NEGATIVE, MAT_SEPIA, out_effect->transform);
    } else if (strcmp(name, "NegativeRed") == 0) {
        float neg_gray[5][5];
        matrix_multiply(MAT_NEGATIVE, MAT_GRAYSCALE, neg_gray);
        matrix_multiply(neg_gray, MAT_RED_BASE, out_effect->transform);
    } else if (strcmp(name, "NegativeHueShift180") == 0) {
        matrix_multiply(MAT_NEGATIVE, MAT_HUESHIFT180, out_effect->transform);
    } else if (strcmp(name, "NegativeHueShift180Variation1") == 0) {
        memcpy(out_effect->transform, MAT_SMART_1, sizeof(MAT_SMART_1));
    } else if (strcmp(name, "NegativeHueShift180Variation2") == 0) {
        memcpy(out_effect->transform, MAT_SMART_2, sizeof(MAT_SMART_2));
    } else if (strcmp(name, "NegativeHueShift180Variation3") == 0) {
        memcpy(out_effect->transform, MAT_SMART_3, sizeof(MAT_SMART_3));
    } else if (strcmp(name, "NegativeHueShift180Variation4") == 0) {
        memcpy(out_effect->transform, MAT_SMART_4, sizeof(MAT_SMART_4));
    } else {
        memcpy(out_effect->transform, MAT_IDENTITY, sizeof(MAT_IDENTITY));
    }
}

bool mag_apply_effect(const char *effect_name) {
    if (!mag_init()) return false;
    MAGCOLOREFFECT effect;
    mag_get_matrix_by_name(effect_name, &effect);
    return g_pfnMagSetColorEffect(&effect);
}

bool mag_restore_default(void) {
    if (!g_isInitialized) return true;
    if (g_pfnMagSetColorEffect) {
        MAGCOLOREFFECT effect;
        memcpy(effect.transform, MAT_IDENTITY, sizeof(MAT_IDENTITY));
        return g_pfnMagSetColorEffect(&effect);
    }
    return false;
}
