#define WIN32_LEAN_AND_MEAN
#define COBJMACROS
#include <windows.h>
#include <physicalmonitorenumerationapi.h>
#include <lowlevelmonitorconfigurationapi.h>
#include <highlevelmonitorconfigurationapi.h>
#include <wbemcli.h>
#include <ole2.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <ctype.h>
#include "brightness.h"
#include "settings.h"
#include "resource.h"

static HWND g_mainHwnd = NULL;
static MonitorInfo g_monitors[MAX_APP_MONITORS];
static int g_monitorCount = 0;
static PHYSICAL_MONITOR g_allocatedPhys[MAX_APP_MONITORS * 2];
static DWORD g_allocatedPhysCount = 0;

static HANDLE g_hWorkerThread = NULL;
static HANDLE g_hWriteEvent = NULL;
static volatile bool g_workerStop = false;
static CRITICAL_SECTION g_cs;
static int g_targetBrightness[MAX_APP_MONITORS];
static bool g_pendingWrite[MAX_APP_MONITORS];

// Forward declaration for flyout notification and logging
void flyout_update_values(void);
void daemon_log(const char *fmt, ...);

static void sanitize_id_w(const wchar_t *in, char *out, size_t max_len) {
    size_t j = 0;
    for (size_t i = 0; in[i] && j + 1 < max_len; i++) {
        if (iswalnum(in[i]) || in[i] == L'_') {
            out[j++] = (char)in[i];
        }
    }
    out[j] = '\0';
}

static void extract_edid_model_w(const wchar_t *id, char *out, size_t max_len) {
    out[0] = '\0';
    if (!id || !*id) return;
    const wchar_t *p = id;
    if (wcsncmp(p, L"\\\\?\\", 4) == 0 || wcsncmp(p, L"\\\\.\\", 4) == 0) p += 4;
    while (*p == L'\\' || *p == L'#' || *p == L'/') p++;
    const wchar_t *next_sep = p;
    while (*next_sep && *next_sep != L'\\' && *next_sep != L'#' && *next_sep != L'/') next_sep++;
    if (*next_sep) {
        p = next_sep + 1;
        while (*p == L'\\' || *p == L'#' || *p == L'/') p++;
        size_t len = 0;
        while (p[len] && p[len] != L'\\' && p[len] != L'#' && p[len] != L'/' && len + 1 < max_len) {
            out[len] = (char)p[len];
            len++;
        }
        out[len] = '\0';
    }
}

static int get_active_desktop_edids(char edids[][32], int max_edids) {
    int count = 0;
    const DWORD ATTACHED_FLAG = 0x00000001; // DISPLAY_DEVICE_ATTACHED_TO_DESKTOP
    const DWORD ACTIVE_FLAG = 0x00000001;   // DISPLAY_DEVICE_ACTIVE

    DISPLAY_DEVICEW adapter;
    memset(&adapter, 0, sizeof(adapter));
    adapter.cb = sizeof(adapter);

    for (DWORD i = 0; EnumDisplayDevicesW(NULL, i, &adapter, 0); i++) {
        if ((adapter.StateFlags & ATTACHED_FLAG) == 0) continue;

        DISPLAY_DEVICEW mon;
        memset(&mon, 0, sizeof(mon));
        mon.cb = sizeof(mon);

        for (DWORD j = 0; EnumDisplayDevicesW(adapter.DeviceName, j, &mon, 0); j++) {
            if ((mon.StateFlags & ACTIVE_FLAG) != 0 && mon.DeviceID[0] != L'\0') {
                char edid[32];
                extract_edid_model_w(mon.DeviceID, edid, sizeof(edid));
                if (edid[0] != '\0' && count < max_edids) {
                    bool already = false;
                    for (int k = 0; k < count; k++) {
                        if (_stricmp(edids[k], edid) == 0) {
                            already = true;
                            break;
                        }
                    }
                    if (!already) {
                        size_t elen = strlen(edid);
                        if (elen >= sizeof(edids[count])) elen = sizeof(edids[count]) - 1;
                        memcpy(edids[count], edid, elen);
                        edids[count][elen] = '\0';
                        count++;
                    }
                }
            }
        }
    }
    return count;
}

static bool extract_json_str_after_key(const char *scope_start, const char *scope_end, const char *key, char *out_val, size_t max_len) {
    char search_key[256];
    snprintf(search_key, sizeof(search_key), "\"%.240s\"", key);
    size_t klen = strlen(search_key);

    const char *p = scope_start;
    while (p && p < scope_end) {
        const char *pos = strstr(p, search_key);
        if (!pos || pos >= scope_end) break;

        // Verify this is a JSON object key (preceded by { or , and whitespace), not a string value
        const char *prev = pos - 1;
        while (prev >= scope_start && (*prev == ' ' || *prev == '\t' || *prev == '\r' || *prev == '\n')) prev--;
        if (prev >= scope_start && *prev != '{' && *prev != ',') {
            p = pos + klen;
            continue;
        }

        const char *after = pos + klen;
        while (after < scope_end && (*after == ' ' || *after == '\t' || *after == '\r' || *after == '\n')) after++;
        if (after < scope_end && *after == ':') {
            after++;
            while (after < scope_end && (*after == ' ' || *after == '\t' || *after == '\r' || *after == '\n')) after++;
            if (after < scope_end && *after == '\"') {
                after++;
                const char *end_str = strchr(after, '\"');
                if (end_str && end_str < scope_end && end_str > after) {
                    size_t nlen = (size_t)(end_str - after);
                    if (nlen >= max_len) nlen = max_len - 1;
                    memcpy(out_val, after, nlen);
                    out_val[nlen] = '\0';
                    return true;
                }
                return false;
            }
        }
        p = pos + klen;
    }
    return false;
}

static void apply_nicknames_from_json(void) {
    wchar_t json_path[MAX_PATH];
    if (!settings_get_path(json_path, MAX_PATH)) return;

    HANDLE hFile = CreateFileW(json_path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hFile == INVALID_HANDLE_VALUE) return;

    DWORD size = GetFileSize(hFile, NULL);
    if (size == 0 || size == INVALID_FILE_SIZE || size > 1024 * 1024) {
        CloseHandle(hFile);
        return;
    }

    char *buf = (char *)malloc(size + 1);
    if (!buf) {
        CloseHandle(hFile);
        return;
    }

    DWORD read = 0;
    if (ReadFile(hFile, buf, size, &read, NULL)) {
        buf[read] = '\0';
        const char *buf_end = buf + read;

        // Scope MonitorNicknames block if present
        const char *nick_start = strstr(buf, "\"MonitorNicknames\"");
        const char *nick_end = buf_end;
        if (nick_start) {
            const char *brace = strchr(nick_start, '{');
            if (brace) {
                nick_start = brace;
                const char *close_brace = strchr(brace, '}');
                if (close_brace) nick_end = close_brace + 1;
            }
        } else {
            nick_start = buf;
        }

        const char *known_start = strstr(buf, "\"KnownMonitors\"");

        // Check for each monitor
        for (int m = 0; m < g_monitorCount; m++) {
            char nick[64] = "";
            if (!extract_json_str_after_key(nick_start, nick_end, g_monitors[m].hardware_id, nick, sizeof(nick)) &&
                g_monitors[m].legacy_id[0]) {
                extract_json_str_after_key(nick_start, nick_end, g_monitors[m].legacy_id, nick, sizeof(nick));
            }

            if (nick[0] == '\0' && known_start) {
                // Fallback: locate monitor entry inside KnownMonitors to extract DisplayName or SystemModelName
                char mon_needle[160];
                snprintf(mon_needle, sizeof(mon_needle), "\"%.127s\"", g_monitors[m].hardware_id);
                const char *mon_pos = strstr(known_start, mon_needle);
                if (mon_pos) {
                    const char *obj_start = mon_pos;
                    while (obj_start > known_start && *obj_start != '{') obj_start--;
                    const char *obj_end = strchr(mon_pos, '}');
                    if (obj_start && obj_end) {
                        if (!extract_json_str_after_key(obj_start, obj_end + 1, "DisplayName", nick, sizeof(nick))) {
                            extract_json_str_after_key(obj_start, obj_end + 1, "SystemModelName", nick, sizeof(nick));
                        }
                    }
                }
            }

            if (nick[0] != '\0') {
                MultiByteToWideChar(CP_UTF8, 0, nick, -1, g_monitors[m].display_name, 64);
            }
        }
    }

    free(buf);
    CloseHandle(hFile);
}

static void detect_wmi_monitors(const char active_edids[][32], int active_edids_count) {
    HRESULT hr = CoInitializeEx(NULL, COINIT_MULTITHREADED);
    bool need_uninit = SUCCEEDED(hr) || hr == S_FALSE;

    IWbemLocator *pLoc = NULL;
    hr = CoCreateInstance(&CLSID_WbemLocator, 0, CLSCTX_INPROC_SERVER, &IID_IWbemLocator, (LPVOID *)&pLoc);
    if (!SUCCEEDED(hr) || !pLoc) {
        if (need_uninit) CoUninitialize();
        return;
    }

    BSTR bstrNamespace = SysAllocString(L"ROOT\\WMI");
    IWbemServices *pSvc = NULL;
    hr = IWbemLocator_ConnectServer(pLoc, bstrNamespace, NULL, NULL, 0, 0, 0, 0, &pSvc);
    SysFreeString(bstrNamespace);

    if (SUCCEEDED(hr) && pSvc) {
        hr = CoSetProxyBlanket((IUnknown *)pSvc, RPC_C_AUTHN_WINNT, RPC_C_AUTHZ_NONE, NULL,
                               RPC_C_AUTHN_LEVEL_CALL, RPC_C_IMP_LEVEL_IMPERSONATE, NULL, EOAC_NONE);

        BSTR bstrQuery = SysAllocString(L"SELECT * FROM WmiMonitorBrightness");
        BSTR bstrWQL = SysAllocString(L"WQL");
        IEnumWbemClassObject *pEnumerator = NULL;
        hr = IWbemServices_ExecQuery(pSvc, bstrWQL, bstrQuery,
                                     WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY, NULL, &pEnumerator);
        SysFreeString(bstrQuery);
        SysFreeString(bstrWQL);

        if (SUCCEEDED(hr) && pEnumerator) {
            IWbemClassObject *pclsObj = NULL;
            ULONG uReturn = 0;

            while (IEnumWbemClassObject_Next(pEnumerator, WBEM_INFINITE, 1, &pclsObj, &uReturn) == S_OK && uReturn == 1) {
                if (g_monitorCount >= MAX_APP_MONITORS) {
                    IWbemClassObject_Release(pclsObj);
                    break;
                }

                VARIANT vtInstance, vtCur;
                VariantInit(&vtInstance);
                VariantInit(&vtCur);

                IWbemClassObject_Get(pclsObj, L"InstanceName", 0, &vtInstance, 0, 0);
                IWbemClassObject_Get(pclsObj, L"CurrentBrightness", 0, &vtCur, 0, 0);

                wchar_t inst[256] = L"InternalDisplay";
                if (vtInstance.vt == VT_BSTR && vtInstance.bstrVal) {
                    wcsncpy(inst, vtInstance.bstrVal, sizeof(inst) / sizeof(wchar_t) - 1);
                }

                // Check clamshell suppression
                char edid[32];
                extract_edid_model_w(inst, edid, sizeof(edid));
                bool attached = true;
                if (active_edids_count > 0 && edid[0] != '\0') {
                    attached = false;
                    for (int k = 0; k < active_edids_count; k++) {
                        if (_stricmp(active_edids[k], edid) == 0) {
                            attached = true;
                            break;
                        }
                    }
                }

                if (attached) {
                    MonitorInfo *m = &g_monitors[g_monitorCount];
                    memset(m, 0, sizeof(MonitorInfo));
                    m->is_internal = true;
                    wcsncpy(m->wmi_instance_name, inst, sizeof(m->wmi_instance_name) / sizeof(wchar_t) - 1);

                    char san[120];
                    sanitize_id_w(inst, san, sizeof(san));
                    snprintf(m->hardware_id, sizeof(m->hardware_id), "WMI_%s", san);
                    strncpy(m->legacy_id, m->hardware_id, sizeof(m->legacy_id) - 1);

                    wcsncpy(m->device_name, L"Built-in", sizeof(m->device_name) / sizeof(wchar_t) - 1);
                    wcsncpy(m->model_name, L"Built-in Display", sizeof(m->model_name) / sizeof(wchar_t) - 1);
                    wcsncpy(m->display_name, L"Built-in Display", sizeof(m->display_name) / sizeof(wchar_t) - 1);

                    int cur = 50;
                    if (vtCur.vt == VT_UI1) cur = vtCur.bVal;
                    else if (vtCur.vt == VT_I4) cur = vtCur.lVal;
                    m->current_brightness = cur;
                    m->min_brightness = 0;
                    m->max_brightness = 100;
                    m->physical_handle = NULL;
                    m->physical_index = 0;

                    g_monitorCount++;
                }

                VariantClear(&vtInstance);
                VariantClear(&vtCur);
                IWbemClassObject_Release(pclsObj);
            }
            IEnumWbemClassObject_Release(pEnumerator);
        }
        IWbemServices_Release(pSvc);
    }
    IWbemLocator_Release(pLoc);
    if (need_uninit) CoUninitialize();
}

static BOOL CALLBACK EnumMonitorsProc(HMONITOR hMon, HDC hdc, LPRECT lpRect, LPARAM lParam) {
    (void)hdc; (void)lpRect; (void)lParam;
    if (g_monitorCount >= MAX_APP_MONITORS) return FALSE;

    DWORD physCount = 0;
    if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, &physCount) || physCount == 0) {
        return TRUE;
    }

    PHYSICAL_MONITOR *pPhys = (PHYSICAL_MONITOR *)malloc(sizeof(PHYSICAL_MONITOR) * physCount);
    if (!pPhys) return TRUE;

    if (!GetPhysicalMonitorsFromHMONITOR(hMon, physCount, pPhys)) {
        free(pPhys);
        return TRUE;
    }

    MONITORINFOEXW mi;
    memset(&mi, 0, sizeof(mi));
    mi.cbSize = sizeof(mi);
    GetMonitorInfoW(hMon, (LPMONITORINFO)&mi);

    for (DWORD i = 0; i < physCount; i++) {
        if (g_allocatedPhysCount < sizeof(g_allocatedPhys) / sizeof(g_allocatedPhys[0])) {
            g_allocatedPhys[g_allocatedPhysCount++] = pPhys[i];
        }

        if (g_monitorCount >= MAX_APP_MONITORS) break;

        // Query PnP device details
        DISPLAY_DEVICEW dd;
        memset(&dd, 0, sizeof(dd));
        dd.cb = sizeof(dd);
        wchar_t stable_id[256] = L"";
        wchar_t model_str[128] = L"";
        if (EnumDisplayDevicesW(mi.szDevice, i, &dd, 0)) {
            wcsncpy(stable_id, dd.DeviceID, sizeof(stable_id) / sizeof(wchar_t) - 1);
            wcsncpy(model_str, dd.DeviceString, sizeof(model_str) / sizeof(wchar_t) - 1);
        }

        // Deduplication: check if internal display already handled via WMI
        char edid[32];
        extract_edid_model_w(stable_id, edid, sizeof(edid));
        bool duplicate = false;
        if (edid[0] != '\0') {
            for (int k = 0; k < g_monitorCount; k++) {
                if (g_monitors[k].is_internal && strstr(g_monitors[k].hardware_id, edid)) {
                    duplicate = true;
                    break;
                }
            }
        }
        if (duplicate) continue;

        MonitorInfo *m = &g_monitors[g_monitorCount];
        memset(m, 0, sizeof(MonitorInfo));
        m->is_internal = false;
        m->has_physical_handle = true;
        m->physical_handle = pPhys[i].hPhysicalMonitor;
        m->physical_index = (int)i;
        daemon_log("EnumMonitorsProc: slot=%d dev='%ls' pPhys[%lu]=%p", g_monitorCount, mi.szDevice, i, pPhys[i].hPhysicalMonitor);
        wcsncpy(m->device_name, mi.szDevice, sizeof(m->device_name) / sizeof(wchar_t) - 1);

        char san_stable[120];
        sanitize_id_w(stable_id, san_stable, sizeof(san_stable));
        if (san_stable[0]) {
            snprintf(m->hardware_id, sizeof(m->hardware_id), "MON_%s", san_stable);
        } else {
            char san_dev[64];
            sanitize_id_w(mi.szDevice, san_dev, sizeof(san_dev));
            snprintf(m->hardware_id, sizeof(m->hardware_id), "PHY_%s_%lu", san_dev, i);
        }

        char san_dev[64];
        sanitize_id_w(mi.szDevice, san_dev, sizeof(san_dev));
        snprintf(m->legacy_id, sizeof(m->legacy_id), "PHY_%s_%lu", san_dev, i);

        // Friendly name
        if (pPhys[i].szPhysicalMonitorDescription[0] &&
            wcsstr(pPhys[i].szPhysicalMonitorDescription, L"Generic") == NULL) {
            wcsncpy(m->model_name, pPhys[i].szPhysicalMonitorDescription, sizeof(m->model_name) / sizeof(wchar_t) - 1);
        } else if (model_str[0] && wcsstr(model_str, L"Generic") == NULL) {
            wcsncpy(m->model_name, model_str, sizeof(m->model_name) / sizeof(wchar_t) - 1);
        } else {
            swprintf(m->model_name, sizeof(m->model_name) / sizeof(wchar_t), L"External Monitor %d", g_monitorCount + 1);
        }
        wcsncpy(m->display_name, m->model_name, sizeof(m->display_name) / sizeof(wchar_t) - 1);

        // Query initial brightness: VCP 0x10 luminance first, then high level
        MC_VCP_CODE_TYPE pvct;
        DWORD vcpCur = 0, vcpMax = 0;
        DWORD minB = 0, curB = 50, maxB = 100;
        if (GetVCPFeatureAndVCPFeatureReply(pPhys[i].hPhysicalMonitor, 0x10, &pvct, &vcpCur, &vcpMax) && vcpMax > 0) {
            m->current_brightness = (int)vcpCur;
            m->min_brightness = 0;
            m->max_brightness = (int)vcpMax;
        } else if (GetMonitorBrightness(pPhys[i].hPhysicalMonitor, &minB, &curB, &maxB)) {
            m->current_brightness = (int)curB;
            m->min_brightness = (int)minB;
            m->max_brightness = (int)maxB;
        } else {
            m->current_brightness = 50;
            m->min_brightness = 0;
            m->max_brightness = 100;
        }

        g_monitorCount++;
    }

    free(pPhys);
    return TRUE;
}

static void cleanup_physical_monitors(void) {
    if (g_allocatedPhysCount > 0) {
        DestroyPhysicalMonitors(g_allocatedPhysCount, g_allocatedPhys);
        g_allocatedPhysCount = 0;
    }
}

void brightness_refresh_monitors(void) {
    EnterCriticalSection(&g_cs);

    cleanup_physical_monitors();
    memset(g_monitors, 0, sizeof(g_monitors));
    g_monitorCount = 0;

    char active_edids[MAX_APP_MONITORS][32];
    int active_edids_count = get_active_desktop_edids(active_edids, MAX_APP_MONITORS);

    // 1. Detect WMI monitors (Internal screen)
    detect_wmi_monitors(active_edids, active_edids_count);

    // 2. Detect external DDC/CI monitors
    EnumDisplayMonitors(NULL, NULL, EnumMonitorsProc, 0);

    // 3. Load user nicknames from settings.json
    apply_nicknames_from_json();

    // 4. Initialize target brightness
    for (int i = 0; i < g_monitorCount; i++) {
        g_targetBrightness[i] = g_monitors[i].current_brightness;
        g_pendingWrite[i] = false;
        daemon_log("brightness: mon[%d] id='%s' name='%ls' internal=%d phys=%p cur=%d",
                   i, g_monitors[i].hardware_id, g_monitors[i].display_name,
                   g_monitors[i].is_internal, g_monitors[i].physical_handle, g_monitors[i].current_brightness);
    }
    daemon_log("brightness: total detected monitors=%d", g_monitorCount);

    LeaveCriticalSection(&g_cs);
}

static void set_wmi_brightness_val(int val) {
    HRESULT hr = CoInitializeEx(NULL, COINIT_MULTITHREADED);
    bool need_uninit = SUCCEEDED(hr) || hr == S_FALSE;

    IWbemLocator *pLoc = NULL;
    hr = CoCreateInstance(&CLSID_WbemLocator, 0, CLSCTX_INPROC_SERVER, &IID_IWbemLocator, (LPVOID *)&pLoc);
    if (!SUCCEEDED(hr) || !pLoc) {
        if (need_uninit) CoUninitialize();
        return;
    }

    BSTR bstrNamespace = SysAllocString(L"ROOT\\WMI");
    IWbemServices *pSvc = NULL;
    hr = IWbemLocator_ConnectServer(pLoc, bstrNamespace, NULL, NULL, 0, 0, 0, 0, &pSvc);
    SysFreeString(bstrNamespace);

    if (SUCCEEDED(hr) && pSvc) {
        CoSetProxyBlanket((IUnknown *)pSvc, RPC_C_AUTHN_WINNT, RPC_C_AUTHZ_NONE, NULL,
                          RPC_C_AUTHN_LEVEL_CALL, RPC_C_IMP_LEVEL_IMPERSONATE, NULL, EOAC_NONE);

        BSTR bstrQuery = SysAllocString(L"SELECT * FROM WmiMonitorBrightnessMethods");
        BSTR bstrWQL = SysAllocString(L"WQL");
        IEnumWbemClassObject *pEnum = NULL;
        hr = IWbemServices_ExecQuery(pSvc, bstrWQL, bstrQuery,
                                     WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY, NULL, &pEnum);
        SysFreeString(bstrQuery);
        SysFreeString(bstrWQL);

        if (SUCCEEDED(hr) && pEnum) {
            IWbemClassObject *pObj = NULL;
            ULONG uRet = 0;
            if (IEnumWbemClassObject_Next(pEnum, WBEM_INFINITE, 1, &pObj, &uRet) == S_OK && uRet == 1) {
                IWbemClassObject *pClass = NULL;
                BSTR bstrClassName = SysAllocString(L"WmiMonitorBrightnessMethods");
                IWbemServices_GetObject(pSvc, bstrClassName, 0, NULL, &pClass, NULL);
                SysFreeString(bstrClassName);

                if (pClass) {
                    IWbemClassObject *pInParamsDef = NULL;
                    BSTR bstrMethod = SysAllocString(L"WmiSetBrightness");
                    IWbemClassObject_GetMethod(pClass, bstrMethod, 0, &pInParamsDef, NULL);

                    if (pInParamsDef) {
                        IWbemClassObject *pInParams = NULL;
                        IWbemClassObject_SpawnInstance(pInParamsDef, 0, &pInParams);

                        if (pInParams) {
                            VARIANT vTimeout, vBright;
                            VariantInit(&vTimeout);
                            vTimeout.vt = VT_UI4;
                            vTimeout.ulVal = 1;

                            VariantInit(&vBright);
                            vBright.vt = VT_UI1;
                            vBright.bVal = (BYTE)val;

                            IWbemClassObject_Put(pInParams, L"Timeout", 0, &vTimeout, 0);
                            IWbemClassObject_Put(pInParams, L"Brightness", 0, &vBright, 0);

                            VARIANT vPath;
                            VariantInit(&vPath);
                            IWbemClassObject_Get(pObj, L"__PATH", 0, &vPath, 0, 0);

                            if (vPath.vt == VT_BSTR) {
                                IWbemServices_ExecMethod(pSvc, vPath.bstrVal, bstrMethod, 0, NULL, pInParams, NULL, NULL);
                            }

                            VariantClear(&vPath);
                            VariantClear(&vTimeout);
                            VariantClear(&vBright);
                            IWbemClassObject_Release(pInParams);
                        }
                        IWbemClassObject_Release(pInParamsDef);
                    }
                    SysFreeString(bstrMethod);
                    IWbemClassObject_Release(pClass);
                }
                IWbemClassObject_Release(pObj);
            }
            IEnumWbemClassObject_Release(pEnum);
        }
        IWbemServices_Release(pSvc);
    }
    IWbemLocator_Release(pLoc);
    if (need_uninit) CoUninitialize();
}

typedef struct {
    wchar_t device_name[32];
    int physical_index;
    HANDLE result;
    bool found;
} ReacquireCtx;

static BOOL CALLBACK ReacquireEnumProc(HMONITOR hMon, HDC hdc, LPRECT lpRect, LPARAM lParam) {
    (void)hdc; (void)lpRect;
    ReacquireCtx *ctx = (ReacquireCtx *)lParam;
    MONITORINFOEXW mi;
    memset(&mi, 0, sizeof(mi));
    mi.cbSize = sizeof(mi);
    if (!GetMonitorInfoW(hMon, (LPMONITORINFO)&mi)) return TRUE;

    if (wcscmp(mi.szDevice, ctx->device_name) == 0) {
        DWORD count = 0;
        if (GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, &count) && count > 0) {
            PHYSICAL_MONITOR *pms = (PHYSICAL_MONITOR *)malloc(sizeof(PHYSICAL_MONITOR) * count);
            if (pms) {
                if (GetPhysicalMonitorsFromHMONITOR(hMon, count, pms)) {
                    int idx = ctx->physical_index;
                    if (idx < 0) idx = 0;
                    if (idx >= (int)count) idx = (int)count - 1;
                    ctx->result = pms[idx].hPhysicalMonitor;
                    ctx->found = true;

                    EnterCriticalSection(&g_cs);
                    for (DWORD k = 0; k < count; k++) {
                        if (g_allocatedPhysCount < sizeof(g_allocatedPhys) / sizeof(g_allocatedPhys[0])) {
                            g_allocatedPhys[g_allocatedPhysCount++] = pms[k];
                        }
                    }
                    LeaveCriticalSection(&g_cs);
                }
                free(pms);
            }
        }
        return FALSE;
    }
    return TRUE;
}

static bool reacquire_physical_handle(int monitor_idx, HANDLE *out_handle) {
    ReacquireCtx ctx;
    memset(&ctx, 0, sizeof(ctx));
    EnterCriticalSection(&g_cs);
    if (monitor_idx < 0 || monitor_idx >= g_monitorCount) {
        LeaveCriticalSection(&g_cs);
        return false;
    }
    wcsncpy(ctx.device_name, g_monitors[monitor_idx].device_name, sizeof(ctx.device_name) / sizeof(wchar_t) - 1);
    ctx.physical_index = g_monitors[monitor_idx].physical_index;
    LeaveCriticalSection(&g_cs);

    EnumDisplayMonitors(NULL, NULL, ReacquireEnumProc, (LPARAM)&ctx);
    if (ctx.found) {
        EnterCriticalSection(&g_cs);
        if (monitor_idx >= 0 && monitor_idx < g_monitorCount) {
            g_monitors[monitor_idx].physical_handle = ctx.result;
            g_monitors[monitor_idx].has_physical_handle = true;
        }
        LeaveCriticalSection(&g_cs);
        *out_handle = ctx.result;
        return true;
    }
    return false;
}

static DWORD WINAPI BrightnessWorkerProc(LPVOID lpParam) {
    (void)lpParam;
    HDESK hDesk = OpenDesktopW(L"Default", 0, FALSE, GENERIC_ALL);
    if (hDesk) SetThreadDesktop(hDesk);

    while (!g_workerStop) {
        WaitForSingleObject(g_hWriteEvent, 200);
        if (g_workerStop) break;

        for (int i = 0; i < MAX_APP_MONITORS; i++) {
            bool pending = false;
            int target = 50;
            bool is_internal = false;
            bool has_phys = false;
            HANDLE phys = NULL;

            EnterCriticalSection(&g_cs);
            if (i < g_monitorCount && g_pendingWrite[i]) {
                pending = true;
                target = g_targetBrightness[i];
                is_internal = g_monitors[i].is_internal;
                has_phys = g_monitors[i].has_physical_handle;
                phys = g_monitors[i].physical_handle;
                g_pendingWrite[i] = false;
            }
            LeaveCriticalSection(&g_cs);

            if (pending) {
                if (is_internal) {
                    daemon_log("BrightnessWorker: setting WMI brightness=%d", target);
                    set_wmi_brightness_val(target);
                } else if (has_phys) {
                    BOOL ok = SetVCPFeature(phys, 0x10, (DWORD)target);
                    DWORD err1 = ok ? 0 : GetLastError();
                    BOOL ok_hl = FALSE;
                    DWORD err2 = 0;
                    if (!ok) {
                        ok_hl = SetMonitorBrightness(phys, (DWORD)target);
                        err2 = ok_hl ? 0 : GetLastError();
                        ok = ok_hl;
                    }
                    if (!ok) {
                        HANDLE fresh = NULL;
                        if (reacquire_physical_handle(i, &fresh)) {
                            ok = SetVCPFeature(fresh, 0x10, (DWORD)target);
                            if (!ok) ok = SetMonitorBrightness(fresh, (DWORD)target);
                            daemon_log("BrightnessWorker: reacquired handle=%p retry=%d", fresh, ok);
                        }
                    }
                    daemon_log("BrightnessWorker: mon[%d] phys=%p target=%d ok=%d (err1=%lu err2=%lu)",
                               i, phys, target, ok, err1, err2);
                    Sleep(30);
                } else {
                    daemon_log("BrightnessWorker: mon[%d] pending target=%d but has_physical_handle=0!", i, target);
                }
            }
        }
    }

    if (hDesk) CloseDesktop(hDesk);
    return 0;
}

void brightness_init(HWND main_hwnd) {
    g_mainHwnd = main_hwnd;
    InitializeCriticalSection(&g_cs);
    g_hWriteEvent = CreateEventW(NULL, FALSE, FALSE, NULL);
    g_workerStop = false;
    brightness_refresh_monitors();

    g_hWorkerThread = CreateThread(NULL, 0, BrightnessWorkerProc, NULL, 0, NULL);
}

void brightness_cleanup(void) {
    g_workerStop = true;
    if (g_hWriteEvent) SetEvent(g_hWriteEvent);
    if (g_hWorkerThread) {
        WaitForSingleObject(g_hWorkerThread, 1000);
        CloseHandle(g_hWorkerThread);
        g_hWorkerThread = NULL;
    }
    if (g_hWriteEvent) {
        CloseHandle(g_hWriteEvent);
        g_hWriteEvent = NULL;
    }

    EnterCriticalSection(&g_cs);
    cleanup_physical_monitors();
    LeaveCriticalSection(&g_cs);
    DeleteCriticalSection(&g_cs);
}

int brightness_get_count(void) {
    int count = 0;
    EnterCriticalSection(&g_cs);
    count = g_monitorCount;
    LeaveCriticalSection(&g_cs);
    return count;
}

bool brightness_get_info(int index, MonitorInfo *out_info) {
    if (!out_info) return false;
    bool ok = false;
    EnterCriticalSection(&g_cs);
    if (index >= 0 && index < g_monitorCount) {
        memcpy(out_info, &g_monitors[index], sizeof(MonitorInfo));
        ok = true;
    }
    LeaveCriticalSection(&g_cs);
    return ok;
}

void brightness_set_direct(int index, int brightness) {
    if (brightness < 0) brightness = 0;
    if (brightness > 100) brightness = 100;

    EnterCriticalSection(&g_cs);
    if (index >= 0 && index < g_monitorCount) {
        g_monitors[index].current_brightness = brightness;
        g_targetBrightness[index] = brightness;
        g_pendingWrite[index] = true;
        SetEvent(g_hWriteEvent);
    }
    LeaveCriticalSection(&g_cs);
}

void brightness_adjust(const char *target_id, int action, int value) {
    EnterCriticalSection(&g_cs);
    bool match_all = (!target_id || !*target_id || _stricmp(target_id, "ALL") == 0);
    int matched = 0;

    for (int i = 0; i < g_monitorCount; i++) {
        if (match_all ||
            _stricmp(g_monitors[i].hardware_id, target_id) == 0 ||
            (g_monitors[i].legacy_id[0] && _stricmp(g_monitors[i].legacy_id, target_id) == 0)) {
            
            int cur = g_monitors[i].current_brightness;
            int next = cur;
            if (action == 0) { // Increase
                next = cur + value;
            } else if (action == 1) { // Decrease
                next = cur - value;
            } else if (action == 2) { // SetTo
                next = value;
            }
            if (next < 0) next = 0;
            if (next > 100) next = 100;

            daemon_log("brightness_adjust: mon[%d] '%ls' %d -> %d", i, g_monitors[i].display_name, cur, next);
            g_monitors[i].current_brightness = next;
            g_targetBrightness[i] = next;
            g_pendingWrite[i] = true;
            matched++;
        }
    }
    if (matched == 0) {
        daemon_log("brightness_adjust: NO monitor matched target_id='%s' (count=%d)", target_id ? target_id : "(null)", g_monitorCount);
    }
    SetEvent(g_hWriteEvent);
    LeaveCriticalSection(&g_cs);

    // Notify UI / flyout
    flyout_update_values();
}
