#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <ctype.h>
#include <stdbool.h>
#include "hotkeys.h"
#include "brightness.h"
#include "flyout.h"
#include "settings.h"

void daemon_log(const char *fmt, ...);

#define MAX_HOTKEY_RULES 32

typedef struct {
    int id;
    char target_id[128];
    int action; // 0: Increase, 1: Decrease, 2: SetTo
    int value;
    bool is_registered;
} HotkeyRule;

static HWND g_hwnd = NULL;
static HotkeyRule g_rules[MAX_HOTKEY_RULES];
static int g_ruleCount = 0;

static void unregister_all_hotkeys(void) {
    if (!g_hwnd) return;
    for (int i = 0; i < g_ruleCount; i++) {
        if (g_rules[i].is_registered) {
            UnregisterHotKey(g_hwnd, g_rules[i].id);
            g_rules[i].is_registered = false;
        }
    }
    g_ruleCount = 0;
}

static uint32_t parse_virtual_key(const char *key) {
    if (!key || !*key) return 0;

    if (_stricmp(key, "RIGHT") == 0) return VK_RIGHT;
    if (_stricmp(key, "LEFT") == 0) return VK_LEFT;
    if (_stricmp(key, "UP") == 0) return VK_UP;
    if (_stricmp(key, "DOWN") == 0) return VK_DOWN;
    if (_stricmp(key, "PAGEUP") == 0 || _stricmp(key, "PRIOR") == 0) return VK_PRIOR;
    if (_stricmp(key, "PAGEDOWN") == 0 || _stricmp(key, "NEXT") == 0) return VK_NEXT;
    if (_stricmp(key, "HOME") == 0) return VK_HOME;
    if (_stricmp(key, "END") == 0) return VK_END;
    if (_stricmp(key, "SPACE") == 0) return VK_SPACE;
    if (_stricmp(key, "ENTER") == 0 || _stricmp(key, "RETURN") == 0) return VK_RETURN;
    if (_stricmp(key, "ESC") == 0 || _stricmp(key, "ESCAPE") == 0) return VK_ESCAPE;
    if (_stricmp(key, "TAB") == 0) return VK_TAB;
    if (_stricmp(key, "BACK") == 0 || _stricmp(key, "BACKSPACE") == 0) return VK_BACK;
    if (_stricmp(key, "INSERT") == 0) return VK_INSERT;
    if (_stricmp(key, "DELETE") == 0) return VK_DELETE;
    if (_stricmp(key, "PLUS") == 0 || strcmp(key, "+") == 0 || strcmp(key, "=") == 0) return VK_OEM_PLUS;
    if (_stricmp(key, "MINUS") == 0 || strcmp(key, "-") == 0) return VK_OEM_MINUS;

    // Function keys F1 - F24
    if ((key[0] == 'f' || key[0] == 'F') && isdigit((unsigned char)key[1])) {
        int fnum = atoi(key + 1);
        if (fnum >= 1 && fnum <= 24) {
            return VK_F1 + (fnum - 1);
        }
    }

    // Single character (A-Z, 0-9)
    if (strlen(key) == 1) {
        char c = (char)toupper((unsigned char)key[0]);
        if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) {
            return (uint32_t)c;
        }
    }

    return 0;
}

static bool parse_accelerator(const char *accel, UINT *out_modifiers, UINT *out_vk) {
    if (!accel || !*accel || !out_modifiers || !out_vk) return false;

    // Decode \u002B or \u002b into '+'
    char decoded[128];
    size_t d = 0;
    for (size_t s = 0; accel[s] && d + 1 < sizeof(decoded); s++) {
        if (accel[s] == '\\' && (accel[s + 1] == 'u' || accel[s + 1] == 'U') &&
            accel[s + 2] == '0' && accel[s + 3] == '0' && accel[s + 4] == '2' &&
            (accel[s + 5] == 'b' || accel[s + 5] == 'B')) {
            decoded[d++] = '+';
            s += 5;
        } else {
            decoded[d++] = accel[s];
        }
    }
    decoded[d] = '\0';

    UINT mods = MOD_NOREPEAT;
    UINT vk = 0;

    // Split on '+' or ' '
    char buf[128];
    strncpy(buf, decoded, sizeof(buf) - 1);
    buf[sizeof(buf) - 1] = '\0';

    char *next_token = NULL;
    char *token = strtok_s(buf, "+ ", &next_token);
    while (token) {
        // Trim leading and trailing spaces
        while (*token == ' ') token++;
        size_t tlen = strlen(token);
        while (tlen > 0 && token[tlen - 1] == ' ') token[--tlen] = '\0';

        if (_stricmp(token, "Ctrl") == 0 || _stricmp(token, "Control") == 0 || _stricmp(token, "CommandOrControl") == 0) {
            mods |= MOD_CONTROL;
        } else if (_stricmp(token, "Alt") == 0 || _stricmp(token, "Menu") == 0) {
            mods |= MOD_ALT;
        } else if (_stricmp(token, "Shift") == 0) {
            mods |= MOD_SHIFT;
        } else if (_stricmp(token, "Win") == 0 || _stricmp(token, "Super") == 0 || _stricmp(token, "Meta") == 0) {
            mods |= MOD_WIN;
        } else {
            vk = parse_virtual_key(token);
        }

        token = strtok_s(NULL, "+ ", &next_token);
    }

    if (vk == 0) return false;

    *out_modifiers = mods;
    *out_vk = vk;
    return true;
}

void hotkeys_init(HWND hwnd) {
    g_hwnd = hwnd;
    hotkeys_reload();
}

void hotkeys_cleanup(void) {
    unregister_all_hotkeys();
    g_hwnd = NULL;
}

void hotkeys_reload(void) {
    unregister_all_hotkeys();

    wchar_t json_path[MAX_PATH];
    if (!settings_get_path(json_path, MAX_PATH)) return;

    HANDLE hFile = CreateFileW(json_path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hFile == INVALID_HANDLE_VALUE) return;

    DWORD size = GetFileSize(hFile, NULL);
    if (size == 0 || size > 1024 * 1024) {
        CloseHandle(hFile);
        return;
    }

    char *buf = (char *)malloc(size + 1);
    if (!buf) {
        CloseHandle(hFile);
        return;
    }

    DWORD read = 0;
    if (!ReadFile(hFile, buf, size, &read, NULL) || read == 0) {
        free(buf);
        CloseHandle(hFile);
        return;
    }
    buf[read] = '\0';
    CloseHandle(hFile);

    // Locate "BrightnessShortcuts"
    const char *shortcuts_arr = strstr(buf, "\"BrightnessShortcuts\"");
    if (!shortcuts_arr) {
        free(buf);
        return;
    }

    const char *open_bracket = strchr(shortcuts_arr, '[');
    if (!open_bracket) {
        free(buf);
        return;
    }

    const char *p = open_bracket + 1;
    while (*p && *p != ']' && g_ruleCount < MAX_HOTKEY_RULES) {
        // Find next object '{'
        const char *obj_start = strchr(p, '{');
        if (!obj_start) break;

        const char *obj_end = strchr(obj_start, '}');
        if (!obj_end) break;

        size_t obj_len = (size_t)(obj_end - obj_start + 1);
        char obj_buf[1024];
        if (obj_len >= sizeof(obj_buf)) obj_len = sizeof(obj_buf) - 1;
        memcpy(obj_buf, obj_start, obj_len);
        obj_buf[obj_len] = '\0';

        // Check Enabled (defaults to true if missing)
        bool enabled = true;
        const char *en_pos = strstr(obj_buf, "\"Enabled\"");
        if (en_pos) {
            const char *colon = strchr(en_pos, ':');
            if (colon) {
                while (*colon == ':' || *colon == ' ' || *colon == '\t') colon++;
                if (_strnicmp(colon, "false", 5) == 0) enabled = false;
            }
        }

        if (enabled) {
            char accel[128] = "";
            char target[128] = "ALL";
            char action_str[32] = "Increase";
            int value = 5;

            // Extract Accelerator
            const char *acc_pos = strstr(obj_buf, "\"Accelerator\"");
            if (acc_pos) {
                const char *quote1 = strchr(acc_pos + 13, '\"');
                if (quote1) {
                    const char *quote2 = strchr(quote1 + 1, '\"');
                    if (quote2) {
                        size_t alen = (size_t)(quote2 - quote1 - 1);
                        if (alen >= sizeof(accel)) alen = sizeof(accel) - 1;
                        memcpy(accel, quote1 + 1, alen);
                        accel[alen] = '\0';
                    }
                }
            }

            // Extract TargetMonitorId
            const char *tgt_pos = strstr(obj_buf, "\"TargetMonitorId\"");
            if (tgt_pos) {
                const char *quote1 = strchr(tgt_pos + 17, '\"');
                if (quote1) {
                    const char *quote2 = strchr(quote1 + 1, '\"');
                    if (quote2) {
                        size_t tlen = (size_t)(quote2 - quote1 - 1);
                        if (tlen >= sizeof(target)) tlen = sizeof(target) - 1;
                        memcpy(target, quote1 + 1, tlen);
                        target[tlen] = '\0';
                    }
                }
            }

            // Extract Action
            const char *act_pos = strstr(obj_buf, "\"Action\"");
            if (act_pos) {
                const char *quote1 = strchr(act_pos + 8, '\"');
                if (quote1) {
                    const char *quote2 = strchr(quote1 + 1, '\"');
                    if (quote2) {
                        size_t actlen = (size_t)(quote2 - quote1 - 1);
                        if (actlen >= sizeof(action_str)) actlen = sizeof(action_str) - 1;
                        memcpy(action_str, quote1 + 1, actlen);
                        action_str[actlen] = '\0';
                    }
                }
            }

            // Extract Value
            const char *val_pos = strstr(obj_buf, "\"Value\"");
            if (val_pos) {
                const char *colon = strchr(val_pos, ':');
                if (colon) {
                    value = atoi(colon + 1);
                }
            }

            UINT mods = 0, vk = 0;
            if (parse_accelerator(accel, &mods, &vk)) {
                int id = 0x9000 + g_ruleCount;
                if (RegisterHotKey(g_hwnd, id, mods, vk)) {
                    g_rules[g_ruleCount].id = id;
                    size_t tlen = strlen(target);
                    if (tlen >= sizeof(g_rules[g_ruleCount].target_id)) tlen = sizeof(g_rules[g_ruleCount].target_id) - 1;
                    memcpy(g_rules[g_ruleCount].target_id, target, tlen);
                    g_rules[g_ruleCount].target_id[tlen] = '\0';

                    if (_stricmp(action_str, "Decrease") == 0 || strcmp(action_str, "1") == 0) {
                        g_rules[g_ruleCount].action = 1;
                    } else if (_stricmp(action_str, "SetTo") == 0 || strcmp(action_str, "2") == 0) {
                        g_rules[g_ruleCount].action = 2;
                    } else {
                        g_rules[g_ruleCount].action = 0; // Increase
                    }

                    g_rules[g_ruleCount].value = value;
                    g_rules[g_ruleCount].is_registered = true;
                    daemon_log("hotkeys: registered id=0x%X accel='%s' vk=0x%X mods=0x%X action=%d val=%d target='%s'",
                               id, accel, vk, mods, g_rules[g_ruleCount].action, value, target);
                    g_ruleCount++;
                } else {
                    daemon_log("hotkeys: FAILED RegisterHotKey accel='%s' vk=0x%X mods=0x%X err=%lu",
                               accel, vk, mods, GetLastError());
                }
            }
        }

        p = obj_end + 1;
    }

    free(buf);
}

void hotkeys_handle_message(WPARAM wParam, LPARAM lParam) {
    (void)lParam;
    int id = (int)wParam;
    bool matched = false;
    for (int i = 0; i < g_ruleCount; i++) {
        if (g_rules[i].is_registered && g_rules[i].id == id) {
            daemon_log("hotkeys_handle_message: matched id=0x%X -> action=%d val=%d target='%s'",
                       id, g_rules[i].action, g_rules[i].value, g_rules[i].target_id);
            brightness_adjust(g_rules[i].target_id, g_rules[i].action, g_rules[i].value);
            matched = true;
            break;
        }
    }
    if (!matched) {
        daemon_log("hotkeys_handle_message: unmatched id=0x%X (ruleCount=%d)", id, g_ruleCount);
    }
}
