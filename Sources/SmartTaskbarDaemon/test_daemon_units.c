#include <windows.h>
#include <stdio.h>
#include <assert.h>
#include <stdbool.h>
#include <math.h>
#include "settings.h"
#include "magnification_effect.h"
#include "taskbar_engine.h"

#define ASSERT_TRUE(x) do { if (!(x)) { printf("[FAIL] Line %d: Assertion '%s' failed.\n", __LINE__, #x); return 1; } } while(0)
#define ASSERT_STR_EQ(a, b) do { if (strcmp((a), (b)) != 0) { printf("[FAIL] Line %d: Expected '%s', got '%s'\n", __LINE__, (b), (a)); return 1; } } while(0)
#define ASSERT_FLOAT_NEAR(a, b) do { if (fabsf((a) - (b)) > 0.0001f) { printf("[FAIL] Line %d: Expected %f, got %f\n", __LINE__, (double)(b), (double)(a)); return 1; } } while(0)

int test_settings_defaults(void) {
    printf("[TEST] Settings defaults...\n");
    AppSettings s;
    settings_get_default(&s);
    ASSERT_TRUE(s.auto_mode == AUTO_MODE_AUTO);
    ASSERT_TRUE(s.show_taskbar_when_exit == true);
    ASSERT_TRUE(s.hide_taskbar_when_fullscreen == true);
    ASSERT_TRUE(s.is_negative_enabled == false);
    ASSERT_STR_EQ(s.active_color_effect, "Negative");
    ASSERT_TRUE(s.click_action == CLICK_ACTION_TOGGLE_INVERSION);
    printf("  -> PASS\n");
    return 0;
}

int test_settings_save_and_load(void) {
    printf("[TEST] Settings save & load roundtrip with extra keys...\n");
    AppSettings write_s;
    settings_get_default(&write_s);
    write_s.auto_mode = AUTO_MODE_NONE;
    write_s.hide_taskbar_when_fullscreen = false;
    write_s.is_negative_enabled = true;
    strncpy(write_s.active_color_effect, "NegativeRed", sizeof(write_s.active_color_effect) - 1);
    write_s.click_action = CLICK_ACTION_TOGGLE_AUTO_MODE;
    write_s.large_screen_threshold = 27.5;

    ASSERT_TRUE(settings_save(&write_s));

    AppSettings read_s;
    ASSERT_TRUE(settings_load(&read_s));

    ASSERT_TRUE(read_s.auto_mode == AUTO_MODE_NONE);
    ASSERT_TRUE(read_s.hide_taskbar_when_fullscreen == false);
    ASSERT_TRUE(read_s.is_negative_enabled == true);
    ASSERT_STR_EQ(read_s.active_color_effect, "NegativeRed");
    ASSERT_TRUE(read_s.click_action == CLICK_ACTION_TOGGLE_AUTO_MODE);
    ASSERT_TRUE(fabs(read_s.large_screen_threshold - 27.5) < 0.1);

    printf("  -> PASS\n");
    return 0;
}

int test_magnification_matrices(void) {
    printf("[TEST] Magnification matrix computations...\n");
    MAGCOLOREFFECT eff;

    // 1. Identity
    mag_get_matrix_by_name("Identity", &eff);
    for (int i = 0; i < 5; i++) {
        for (int j = 0; j < 5; j++) {
            ASSERT_FLOAT_NEAR(eff.transform[i][j], (i == j ? 1.0f : 0.0f));
        }
    }

    // 2. Negative
    mag_get_matrix_by_name("Negative", &eff);
    ASSERT_FLOAT_NEAR(eff.transform[0][0], -1.0f);
    ASSERT_FLOAT_NEAR(eff.transform[1][1], -1.0f);
    ASSERT_FLOAT_NEAR(eff.transform[2][2], -1.0f);
    ASSERT_FLOAT_NEAR(eff.transform[4][0], 1.0f);
    ASSERT_FLOAT_NEAR(eff.transform[4][1], 1.0f);
    ASSERT_FLOAT_NEAR(eff.transform[4][2], 1.0f);

    // 3. GrayScale
    mag_get_matrix_by_name("GrayScale", &eff);
    ASSERT_FLOAT_NEAR(eff.transform[0][0], 0.3f);
    ASSERT_FLOAT_NEAR(eff.transform[1][0], 0.6f);
    ASSERT_FLOAT_NEAR(eff.transform[2][0], 0.1f);

    // 4. Smart Invert 1 (NegativeHueShift180Variation1)
    mag_get_matrix_by_name("NegativeHueShift180Variation1", &eff);
    ASSERT_FLOAT_NEAR(eff.transform[0][0], 1.0f);
    ASSERT_FLOAT_NEAR(eff.transform[0][1], -1.0f);
    ASSERT_FLOAT_NEAR(eff.transform[0][2], -1.0f);
    ASSERT_FLOAT_NEAR(eff.transform[4][0], 1.0f);

    // 5. Smart Invert 3 (NegativeHueShift180Variation3)
    mag_get_matrix_by_name("NegativeHueShift180Variation3", &eff);
    ASSERT_FLOAT_NEAR(eff.transform[0][0], 1.089508f);
    ASSERT_FLOAT_NEAR(eff.transform[1][0], -1.81771779f);

    printf("  -> PASS\n");
    return 0;
}

int test_settings_reload_event(void) {
    printf("[TEST] Settings reload event signaling (Daemon and UI channels)...\n");
    HANDLE hDaemonEvent = CreateEventW(NULL, FALSE, FALSE, SETTINGS_RELOAD_EVENT_NAME);
    ASSERT_TRUE(hDaemonEvent != NULL);
    HANDLE hUIEvent = CreateEventW(NULL, FALSE, FALSE, SETTINGS_UI_RELOAD_EVENT_NAME);
    ASSERT_TRUE(hUIEvent != NULL);

    // Initially unsignaled
    ASSERT_TRUE(WaitForSingleObject(hDaemonEvent, 0) == WAIT_TIMEOUT);
    ASSERT_TRUE(WaitForSingleObject(hUIEvent, 0) == WAIT_TIMEOUT);

    // 1. Signal daemon reload exclusively
    settings_signal_daemon_reload();
    ASSERT_TRUE(WaitForSingleObject(hDaemonEvent, 100) == WAIT_OBJECT_0);
    ASSERT_TRUE(WaitForSingleObject(hUIEvent, 0) == WAIT_TIMEOUT);

    // 2. Signal UI reload exclusively
    settings_signal_ui_reload();
    ASSERT_TRUE(WaitForSingleObject(hUIEvent, 100) == WAIT_OBJECT_0);
    ASSERT_TRUE(WaitForSingleObject(hDaemonEvent, 0) == WAIT_TIMEOUT);

    // 3. Broadcast reload
    settings_signal_reload();
    ASSERT_TRUE(WaitForSingleObject(hDaemonEvent, 100) == WAIT_OBJECT_0);
    ASSERT_TRUE(WaitForSingleObject(hUIEvent, 100) == WAIT_OBJECT_0);

    CloseHandle(hDaemonEvent);
    CloseHandle(hUIEvent);
    printf("  -> PASS\n");
    return 0;
}

int test_empty_json_handling(void) {
    printf("[TEST] Edge Case: Saving into empty JSON object '{}'...\n");
    wchar_t path[MAX_PATH];
    settings_get_path(path, MAX_PATH);

    wchar_t bak[MAX_PATH];
    swprintf(bak, MAX_PATH, L"%ls.bak", path);
    CopyFileW(path, bak, FALSE);

    // Write empty object
    FILE *f = _wfopen(path, L"wb");
    ASSERT_TRUE(f != NULL);
    fputs("{}", f);
    fclose(f);

    AppSettings s;
    settings_get_default(&s);
    s.auto_mode = AUTO_MODE_AUTO;
    s.is_negative_enabled = true;
    ASSERT_TRUE(settings_save(&s));

    // Read back and inspect
    f = _wfopen(path, L"rb");
    ASSERT_TRUE(f != NULL);
    char buf[4096];
    size_t n = fread(buf, 1, sizeof(buf) - 1, f);
    buf[n] = '\0';
    fclose(f);

    // Must NOT start with comma inside brace
    ASSERT_TRUE(strstr(buf, "{,") == NULL);
    ASSERT_TRUE(strstr(buf, "{ ,") == NULL);

    // Verify it loads back cleanly
    AppSettings loaded;
    ASSERT_TRUE(settings_load(&loaded));
    ASSERT_TRUE(loaded.is_negative_enabled == true);
    ASSERT_TRUE(loaded.auto_mode == AUTO_MODE_AUTO);

    // Restore backup
    MoveFileExW(bak, path, MOVEFILE_REPLACE_EXISTING);
    printf("  -> PASS\n");
    return 0;
}

int test_whitespace_json_handling(void) {
    printf("[TEST] Edge Case: Saving into whitespace JSON object '{   \\n  }'...\n");
    wchar_t path[MAX_PATH];
    settings_get_path(path, MAX_PATH);

    wchar_t bak[MAX_PATH];
    swprintf(bak, MAX_PATH, L"%ls.bak", path);
    CopyFileW(path, bak, FALSE);

    FILE *f = _wfopen(path, L"wb");
    ASSERT_TRUE(f != NULL);
    fputs("{\n   \t  \n}", f);
    fclose(f);

    AppSettings s;
    settings_get_default(&s);
    s.auto_mode = AUTO_MODE_NONE;
    ASSERT_TRUE(settings_save(&s));

    f = _wfopen(path, L"rb");
    ASSERT_TRUE(f != NULL);
    char buf[4096];
    size_t n = fread(buf, 1, sizeof(buf) - 1, f);
    buf[n] = '\0';
    fclose(f);

    ASSERT_TRUE(strstr(buf, "{,") == NULL);
    ASSERT_TRUE(strstr(buf, "{ ,") == NULL);

    AppSettings loaded;
    ASSERT_TRUE(settings_load(&loaded));
    ASSERT_TRUE(loaded.auto_mode == AUTO_MODE_NONE);

    MoveFileExW(bak, path, MOVEFILE_REPLACE_EXISTING);
    printf("  -> PASS\n");
    return 0;
}

int test_corrupted_json_fallback(void) {
    printf("[TEST] Edge Case: Loading malformed / corrupted JSON falls back to defaults...\n");
    wchar_t path[MAX_PATH];
    settings_get_path(path, MAX_PATH);

    wchar_t bak[MAX_PATH];
    swprintf(bak, MAX_PATH, L"%ls.bak", path);
    CopyFileW(path, bak, FALSE);

    FILE *f = _wfopen(path, L"wb");
    ASSERT_TRUE(f != NULL);
    fputs("<<corrupted random binary data \x01\x02\xFF>>", f);
    fclose(f);

    AppSettings loaded;
    // Loading corrupted JSON should not crash; it safely populates default values
    settings_load(&loaded);
    ASSERT_TRUE(loaded.auto_mode == AUTO_MODE_AUTO);
    ASSERT_TRUE(loaded.is_negative_enabled == false);
    ASSERT_STR_EQ(loaded.active_color_effect, "Negative");

    MoveFileExW(bak, path, MOVEFILE_REPLACE_EXISTING);
    printf("  -> PASS\n");
    return 0;
}

int main(void) {
    printf("===========================================\n");
    printf("SmartTaskbarDaemon Unit & Regression Tests\n");
    printf("===========================================\n\n");

    int res = 0;
    res |= test_settings_defaults();
    res |= test_magnification_matrices();
    res |= test_settings_reload_event();
    res |= test_settings_save_and_load();
    res |= test_empty_json_handling();
    res |= test_whitespace_json_handling();
    res |= test_corrupted_json_fallback();

    if (res == 0) {
        printf("\n[ALL TESTS PASSED SUCCESSFULLY!]\n");
    } else {
        printf("\n[SOME TESTS FAILED!]\n");
    }
    return res;
}
