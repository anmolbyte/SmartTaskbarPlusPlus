#include "settings.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <shlobj.h>

void settings_get_default(AppSettings *settings) {
    if (!settings) return;
    settings->auto_mode = AUTO_MODE_AUTO;
    settings->show_taskbar_when_exit = true;
    settings->hide_taskbar_when_fullscreen = true;
    settings->start_on_login = false;
    settings->large_screen_threshold = 42.0;
    settings->disable_large_screen_override = true;
    settings->is_negative_enabled = false;
    strncpy(settings->active_color_effect, "Negative", sizeof(settings->active_color_effect) - 1);
    settings->active_color_effect[sizeof(settings->active_color_effect) - 1] = '\0';
    settings->click_action = CLICK_ACTION_TOGGLE_INVERSION;
    settings->detection_mode = SCREEN_DETECTION_PRIMARY;
}

bool settings_get_path(wchar_t *out_path, size_t max_len) {
    if (!out_path || max_len == 0) return false;

    // First check next to executable
    wchar_t exe_path[MAX_PATH];
    if (GetModuleFileNameW(NULL, exe_path, MAX_PATH) > 0) {
        wchar_t *last_slash = wcsrchr(exe_path, L'\\');
        if (last_slash) {
            *last_slash = L'\0';
            wchar_t local_json[MAX_PATH];
            swprintf(local_json, MAX_PATH, L"%ls\\settings.json", exe_path);
            DWORD attrs = GetFileAttributesW(local_json);
            if (attrs != INVALID_FILE_ATTRIBUTES && !(attrs & FILE_ATTRIBUTE_DIRECTORY)) {
                wcsncpy(out_path, local_json, max_len - 1);
                out_path[max_len - 1] = L'\0';
                return true;
            }
        }
    }

    // Default to %APPDATA%\SmartTaskbar\settings.json
    wchar_t appdata[MAX_PATH];
    if (SUCCEEDED(SHGetFolderPathW(NULL, CSIDL_APPDATA, NULL, 0, appdata))) {
        wchar_t dir[MAX_PATH];
        swprintf(dir, MAX_PATH, L"%ls\\SmartTaskbar", appdata);
        CreateDirectoryW(dir, NULL);

        swprintf(out_path, max_len, L"%ls\\settings.json", dir);
        return true;
    }

    return false;
}

static bool json_find_value(const char *json, const char *key, const char **val_start, const char **val_end) {
    if (!json || !key || !val_start || !val_end) return false;

    char search_key[128];
    snprintf(search_key, sizeof(search_key), "\"%s\"", key);
    size_t key_len = strlen(search_key);

    const char *p = json;
    while ((p = strstr(p, search_key)) != NULL) {
        // Ensure this is a key (check preceding non-whitespace is { or ,)
        const char *before = p - 1;
        while (before >= json && (*before == ' ' || *before == '\t' || *before == '\r' || *before == '\n')) {
            before--;
        }
        if (before >= json && *before != '{' && *before != ',') {
            p += key_len;
            continue;
        }

        // Advance past key
        const char *after = p + key_len;
        while (*after == ' ' || *after == '\t' || *after == '\r' || *after == '\n') after++;
        if (*after != ':') {
            p += key_len;
            continue;
        }
        after++; // Skip ':'
        while (*after == ' ' || *after == '\t' || *after == '\r' || *after == '\n') after++;

        *val_start = after;

        // Find end of value
        if (*after == '\"') {
            // String value
            const char *end = after + 1;
            while (*end && *end != '\"') {
                if (*end == '\\' && *(end + 1)) end += 2;
                else end++;
            }
            if (*end == '\"') end++;
            *val_end = end;
            return true;
        } else if (*after == '{' || *after == '[') {
            // Complex structure, find matching bracket
            char open_char = *after;
            char close_char = (open_char == '{') ? '}' : ']';
            int depth = 1;
            const char *end = after + 1;
            bool in_str = false;
            while (*end && depth > 0) {
                if (*end == '\"' && (end == after + 1 || *(end - 1) != '\\')) {
                    in_str = !in_str;
                } else if (!in_str) {
                    if (*end == open_char) depth++;
                    else if (*end == close_char) depth--;
                }
                end++;
            }
            *val_end = end;
            return true;
        } else {
            // Primitive (boolean, number, null)
            const char *end = after;
            while (*end && *end != ',' && *end != '}' && *end != ']' &&
                   *end != ' ' && *end != '\t' && *end != '\r' && *end != '\n') {
                end++;
            }
            *val_end = end;
            return true;
        }
    }
    return false;
}

static bool json_get_bool(const char *json, const char *key, bool def_val) {
    const char *start = NULL;
    const char *end = NULL;
    if (json_find_value(json, key, &start, &end)) {
        size_t len = (size_t)(end - start);
        if (len == 4 && _strnicmp(start, "true", 4) == 0) return true;
        if (len == 5 && _strnicmp(start, "false", 5) == 0) return false;
        if (len == 1 && *start == '1') return true;
        if (len == 1 && *start == '0') return false;
    }
    return def_val;
}

static double json_get_double(const char *json, const char *key, double def_val) {
    const char *start = NULL;
    const char *end = NULL;
    if (json_find_value(json, key, &start, &end)) {
        char buf[64];
        size_t len = (size_t)(end - start);
        if (len < sizeof(buf)) {
            memcpy(buf, start, len);
            buf[len] = '\0';
            char *stop = NULL;
            double v = strtod(buf, &stop);
            if (stop != buf) return v;
        }
    }
    return def_val;
}

static void json_get_string(const char *json, const char *key, char *out_val, size_t max_len, const char *def_val) {
    if (!out_val || max_len == 0) return;
    const char *start = NULL;
    const char *end = NULL;
    if (json_find_value(json, key, &start, &end)) {
        if (*start == '\"' && end > start && *(end - 1) == '\"') {
            start++;
            end--;
        }
        if (end > start) {
            size_t len = (size_t)(end - start);
            if (len >= max_len) len = max_len - 1;
            memcpy(out_val, start, len);
            out_val[len] = '\0';
            return;
        } else if (end == start) {
            out_val[0] = '\0';
            return;
        }
    }
    if (def_val) {
        strncpy(out_val, def_val, max_len - 1);
        out_val[max_len - 1] = '\0';
    } else {
        out_val[0] = '\0';
    }
}

bool settings_load(AppSettings *settings) {
    if (!settings) return false;
    settings_get_default(settings);

    wchar_t path[MAX_PATH];
    if (!settings_get_path(path, MAX_PATH)) return false;

    HANDLE hMutex = CreateMutexW(NULL, FALSE, SETTINGS_FILE_MUTEX_NAME);
    if (hMutex) {
        WaitForSingleObject(hMutex, 2000);
    }

    HANDLE hFile = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hFile == INVALID_HANDLE_VALUE) {
        if (hMutex) {
            ReleaseMutex(hMutex);
            CloseHandle(hMutex);
        }
        return false;
    }

    DWORD file_size = GetFileSize(hFile, NULL);
    if (file_size == 0 || file_size == INVALID_FILE_SIZE || file_size > 1024 * 1024) {
        CloseHandle(hFile);
        if (hMutex) {
            ReleaseMutex(hMutex);
            CloseHandle(hMutex);
        }
        return false;
    }

    char *buf = (char *)malloc(file_size + 1);
    if (!buf) {
        CloseHandle(hFile);
        if (hMutex) {
            ReleaseMutex(hMutex);
            CloseHandle(hMutex);
        }
        return false;
    }

    DWORD bytes_read = 0;
    BOOL read_ok = ReadFile(hFile, buf, file_size, &bytes_read, NULL);
    CloseHandle(hFile);

    if (hMutex) {
        ReleaseMutex(hMutex);
        CloseHandle(hMutex);
    }

    if (!read_ok || bytes_read == 0) {
        free(buf);
        return false;
    }
    buf[bytes_read] = '\0';

    // Parse settings
    char auto_mode_str[32];
    json_get_string(buf, "AutoModeType", auto_mode_str, sizeof(auto_mode_str), "Auto");
    if (_stricmp(auto_mode_str, "None") == 0 || strcmp(auto_mode_str, "0") == 0) {
        settings->auto_mode = AUTO_MODE_NONE;
    } else {
        settings->auto_mode = AUTO_MODE_AUTO;
    }

    settings->show_taskbar_when_exit = json_get_bool(buf, "ShowTaskbarWhenExit", true);
    settings->hide_taskbar_when_fullscreen = json_get_bool(buf, "HideTaskbarWhenFullscreen", true);
    settings->start_on_login = json_get_bool(buf, "StartOnLogin", false);
    settings->large_screen_threshold = json_get_double(buf, "LargeScreenThreshold", 42.0);
    settings->disable_large_screen_override = json_get_bool(buf, "DisableLargeScreenOverride", true);
    settings->is_negative_enabled = json_get_bool(buf, "IsNegativeModeEnabled", false);

    json_get_string(buf, "ActiveColorEffect", settings->active_color_effect, sizeof(settings->active_color_effect), "Negative");

    char click_action_str[64];
    json_get_string(buf, "ClickAction", click_action_str, sizeof(click_action_str), "ToggleInversion");
    if (_stricmp(click_action_str, "ToggleInversion") == 0 || strcmp(click_action_str, "0") == 0) {
        settings->click_action = CLICK_ACTION_TOGGLE_INVERSION;
    } else if (_stricmp(click_action_str, "ToggleAutoMode") == 0 || strcmp(click_action_str, "1") == 0) {
        settings->click_action = CLICK_ACTION_TOGGLE_AUTO_MODE;
    } else if (_stricmp(click_action_str, "SetShowMode") == 0 || strcmp(click_action_str, "2") == 0) {
        settings->click_action = CLICK_ACTION_SET_SHOW_MODE;
    } else if (_stricmp(click_action_str, "SetHideMode") == 0 || strcmp(click_action_str, "3") == 0) {
        settings->click_action = CLICK_ACTION_SET_HIDE_MODE;
    } else if (_stricmp(click_action_str, "None") == 0 || strcmp(click_action_str, "4") == 0) {
        settings->click_action = CLICK_ACTION_NONE;
    } else {
        settings->click_action = CLICK_ACTION_TOGGLE_INVERSION;
    }

    char det_str[64];
    json_get_string(buf, "LargeScreenDetectionMode", det_str, sizeof(det_str), "PrimaryOnly");
    if (_stricmp(det_str, "AnyMonitor") == 0 || strcmp(det_str, "1") == 0) {
        settings->detection_mode = SCREEN_DETECTION_ANY;
    } else {
        settings->detection_mode = SCREEN_DETECTION_PRIMARY;
    }

    free(buf);
    return true;
}

static char* replace_or_insert_key(const char *orig_json, const char *key, const char *new_val_fmt) {
    const char *start = NULL;
    const char *end = NULL;
    if (orig_json && json_find_value(orig_json, key, &start, &end)) {
        size_t prefix_len = (size_t)(start - orig_json);
        size_t suffix_len = strlen(end);
        size_t val_len = strlen(new_val_fmt);
        char *result = (char *)malloc(prefix_len + val_len + suffix_len + 1);
        if (!result) return NULL;
        memcpy(result, orig_json, prefix_len);
        memcpy(result + prefix_len, new_val_fmt, val_len);
        memcpy(result + prefix_len + val_len, end, suffix_len);
        result[prefix_len + val_len + suffix_len] = '\0';
        return result;
    }

    // Key not found: insert before last '}'
    if (!orig_json || strlen(orig_json) < 2) {
        // Build fresh json
        char *result = (char *)malloc(256);
        if (!result) return NULL;
        snprintf(result, 256, "{\n  \"%s\": %s\n}", key, new_val_fmt);
        return result;
    }

    const char *last_brace = strrchr(orig_json, '}');
    if (!last_brace) return _strdup(orig_json);

    // Check if there is already a property inside the object
    const char *prev = last_brace - 1;
    while (prev >= orig_json && (*prev == ' ' || *prev == '\t' || *prev == '\r' || *prev == '\n')) {
        prev--;
    }
    bool need_comma = (prev >= orig_json && *prev != '{' && *prev != '[' && *prev != ',');

    size_t prefix_len = (size_t)(last_brace - orig_json);
    char insert_str[256];
    if (need_comma) {
        snprintf(insert_str, sizeof(insert_str), ",\n  \"%s\": %s\n", key, new_val_fmt);
    } else {
        snprintf(insert_str, sizeof(insert_str), "\n  \"%s\": %s\n", key, new_val_fmt);
    }
    size_t insert_len = strlen(insert_str);
    size_t suffix_len = strlen(last_brace);

    char *result = (char *)malloc(prefix_len + insert_len + suffix_len + 1);
    if (!result) return NULL;
    memcpy(result, orig_json, prefix_len);
    memcpy(result + prefix_len, insert_str, insert_len);
    memcpy(result + prefix_len + insert_len, last_brace, suffix_len);
    result[prefix_len + insert_len + suffix_len] = '\0';
    return result;
}

bool settings_save(const AppSettings *settings) {
    if (!settings) return false;

    wchar_t path[MAX_PATH];
    if (!settings_get_path(path, MAX_PATH)) return false;

    HANDLE hMutex = CreateMutexW(NULL, FALSE, SETTINGS_FILE_MUTEX_NAME);
    if (hMutex) {
        WaitForSingleObject(hMutex, 2000);
    }

    // Read existing content if possible to preserve extra fields (e.g., BrightnessShortcuts)
    char *current_json = NULL;
    HANDLE hFile = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hFile != INVALID_HANDLE_VALUE) {
        DWORD size = GetFileSize(hFile, NULL);
        if (size > 0 && size < 1024 * 1024) {
            current_json = (char *)malloc(size + 1);
            if (current_json) {
                DWORD read = 0;
                if (ReadFile(hFile, current_json, size, &read, NULL)) {
                    current_json[read] = '\0';
                } else {
                    free(current_json);
                    current_json = NULL;
                }
            }
        }
        CloseHandle(hFile);
    }

    if (!current_json) {
        // Construct basic JSON
        const char *base = "{\n"
                           "  \"AutoModeType\": \"Auto\",\n"
                           "  \"ShowTaskbarWhenExit\": true,\n"
                           "  \"HideTaskbarWhenFullscreen\": true,\n"
                           "  \"StartOnLogin\": false,\n"
                           "  \"LargeScreenThreshold\": 42.0,\n"
                           "  \"DisableLargeScreenOverride\": true,\n"
                           "  \"IsNegativeModeEnabled\": false,\n"
                           "  \"ActiveColorEffect\": \"Negative\",\n"
                           "  \"ClickAction\": \"ToggleInversion\",\n"
                           "  \"LargeScreenDetectionMode\": \"PrimaryOnly\",\n"
                           "  \"CheckForUpdates\": true,\n"
                           "  \"UpdateFrequency\": \"Day\"\n"
                           "}";
        current_json = _strdup(base);
    }

    // Update keys one by one
    char val_buf[128];

    // AutoModeType
    snprintf(val_buf, sizeof(val_buf), "\"%s\"", settings->auto_mode == AUTO_MODE_AUTO ? "Auto" : "None");
    char *tmp = replace_or_insert_key(current_json, "AutoModeType", val_buf);
    free(current_json);
    current_json = tmp;

    // ShowTaskbarWhenExit
    snprintf(val_buf, sizeof(val_buf), "%s", settings->show_taskbar_when_exit ? "true" : "false");
    tmp = replace_or_insert_key(current_json, "ShowTaskbarWhenExit", val_buf);
    free(current_json);
    current_json = tmp;

    // HideTaskbarWhenFullscreen
    snprintf(val_buf, sizeof(val_buf), "%s", settings->hide_taskbar_when_fullscreen ? "true" : "false");
    tmp = replace_or_insert_key(current_json, "HideTaskbarWhenFullscreen", val_buf);
    free(current_json);
    current_json = tmp;

    // StartOnLogin
    snprintf(val_buf, sizeof(val_buf), "%s", settings->start_on_login ? "true" : "false");
    tmp = replace_or_insert_key(current_json, "StartOnLogin", val_buf);
    free(current_json);
    current_json = tmp;

    // LargeScreenThreshold
    snprintf(val_buf, sizeof(val_buf), "%.1f", settings->large_screen_threshold);
    tmp = replace_or_insert_key(current_json, "LargeScreenThreshold", val_buf);
    free(current_json);
    current_json = tmp;

    // DisableLargeScreenOverride
    snprintf(val_buf, sizeof(val_buf), "%s", settings->disable_large_screen_override ? "true" : "false");
    tmp = replace_or_insert_key(current_json, "DisableLargeScreenOverride", val_buf);
    free(current_json);
    current_json = tmp;

    // IsNegativeModeEnabled
    snprintf(val_buf, sizeof(val_buf), "%s", settings->is_negative_enabled ? "true" : "false");
    tmp = replace_or_insert_key(current_json, "IsNegativeModeEnabled", val_buf);
    free(current_json);
    current_json = tmp;

    // ActiveColorEffect
    snprintf(val_buf, sizeof(val_buf), "\"%s\"", settings->active_color_effect);
    tmp = replace_or_insert_key(current_json, "ActiveColorEffect", val_buf);
    free(current_json);
    current_json = tmp;

    // ClickAction
    const char *action_str = "ToggleInversion";
    switch (settings->click_action) {
        case CLICK_ACTION_TOGGLE_AUTO_MODE: action_str = "ToggleAutoMode"; break;
        case CLICK_ACTION_SET_SHOW_MODE:    action_str = "SetShowMode"; break;
        case CLICK_ACTION_SET_HIDE_MODE:    action_str = "SetHideMode"; break;
        case CLICK_ACTION_NONE:             action_str = "None"; break;
        default:                            action_str = "ToggleInversion"; break;
    }
    snprintf(val_buf, sizeof(val_buf), "\"%s\"", action_str);
    tmp = replace_or_insert_key(current_json, "ClickAction", val_buf);
    free(current_json);
    current_json = tmp;

    // LargeScreenDetectionMode
    snprintf(val_buf, sizeof(val_buf), "\"%s\"", settings->detection_mode == SCREEN_DETECTION_ANY ? "AnyMonitor" : "PrimaryOnly");
    tmp = replace_or_insert_key(current_json, "LargeScreenDetectionMode", val_buf);
    free(current_json);
    current_json = tmp;

    // Atomic write to path.tmp then MoveFileEx
    wchar_t tmp_path[MAX_PATH];
    swprintf(tmp_path, MAX_PATH, L"%ls.tmp", path);

    HANDLE hOut = CreateFileW(tmp_path, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    bool write_success = false;
    if (hOut != INVALID_HANDLE_VALUE) {
        DWORD len = (DWORD)strlen(current_json);
        DWORD written = 0;
        if (WriteFile(hOut, current_json, len, &written, NULL) && written == len) {
            write_success = true;
        }
        CloseHandle(hOut);
    }

    if (write_success) {
        if (!MoveFileExW(tmp_path, path, MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH)) {
            write_success = false;
            DeleteFileW(tmp_path);
        }
    } else {
        DeleteFileW(tmp_path);
    }

    free(current_json);

    if (hMutex) {
        ReleaseMutex(hMutex);
        CloseHandle(hMutex);
    }

    // When the daemon saves settings, notify any open Settings UI
    settings_signal_ui_reload();

    return write_success;
}

void settings_signal_ui_reload(void) {
    HANDLE hEvent = OpenEventW(EVENT_MODIFY_STATE, FALSE, SETTINGS_UI_RELOAD_EVENT_NAME);
    if (hEvent) {
        SetEvent(hEvent);
        CloseHandle(hEvent);
    }
}

void settings_signal_daemon_reload(void) {
    HANDLE hEvent = OpenEventW(EVENT_MODIFY_STATE, FALSE, SETTINGS_RELOAD_EVENT_NAME);
    if (hEvent) {
        SetEvent(hEvent);
        CloseHandle(hEvent);
    }
}

void settings_signal_reload(void) {
    settings_signal_daemon_reload();
    settings_signal_ui_reload();
}
