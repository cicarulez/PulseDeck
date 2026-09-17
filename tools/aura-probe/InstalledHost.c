/* Passive, on-demand COM destination. No ASUS SDK, hardware or control calls. */
#define COBJMACROS
#include <windows.h>
#include <shlobj.h>
#include <stdio.h>
#include <wchar.h>
#include "DiscoveryHal.h"

static void Report(const wchar_t *path, const char *state, HRESULT hr) {
    wchar_t temporary[MAX_PATH];
    if (swprintf(temporary, MAX_PATH, L"%ls.tmp", path) < 0) return;
    FILE *file = _wfopen(temporary, L"wb");
    if (!file) return;
    ProbeStats s = GetProbeStats();
    DWORD session = 0;
    ProcessIdToSessionId(GetCurrentProcessId(), &session);
    SYSTEMTIME utc; GetSystemTime(&utc);
    fprintf(file, "{\"scope\":\"passive installed HAL; metadata only\",\"state\":\"%s\","
        "\"utc\":\"%04u-%02u-%02uT%02u:%02u:%02uZ\",\"pid\":%lu,\"session\":%lu,"
        "\"hresult\":\"0x%08lx\",\"activations\":%ld,\"enumerations\":%ld,"
        "\"capabilities\":%ld,\"effectRequests\":%ld,\"syncRequests\":%ld,"
        "\"halRefs\":%ld,\"deviceRefs\":%ld,\"factoryRefs\":%ld,"
        "\"deviceType\":%lu,\"lastEffectMethod\":%ld,\"lastEffectId\":%lu,"
        "\"lastEffectCount\":%lu,\"lastEffectVariant\":%lu}", state,
        utc.wYear, utc.wMonth, utc.wDay, utc.wHour, utc.wMinute, utc.wSecond,
        GetCurrentProcessId(), session, hr, s.activations, s.enumerations,
        s.capabilities, s.effect_requests, s.sync_requests, s.hal_refs, s.device_refs, s.factory_refs,
        PROBE_DEVICE_TYPE, s.last_effect_method, s.last_effect_id, s.last_effect_count, s.last_effect_variant);
    fclose(file);
    MoveFileExW(temporary, path, MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH);
}

int WINAPI WinMain(HINSTANCE instance, HINSTANCE previous, LPSTR args, int show) {
    (void)instance; (void)previous; (void)show;
    if (_stricmp(args, "-Embedding") != 0) return 2;
    wchar_t stop[MAX_PATH], directory[MAX_PATH], report[MAX_PATH];
    DWORD size = GetModuleFileNameW(NULL, stop, MAX_PATH);
    if (!size || size >= MAX_PATH) return 3;
    wchar_t *separator = wcsrchr(stop, L'\\');
    if (!separator) return 3;
    *separator = 0;
    if (wcslen(stop) + 16 >= MAX_PATH) return 3;
    wcscat(stop, L"\\uninstall.stop");
    if (GetFileAttributesW(stop) != INVALID_FILE_ATTRIBUTES) return 4;
    if (FAILED(SHGetFolderPathW(NULL, CSIDL_LOCAL_APPDATA | CSIDL_FLAG_CREATE,
        NULL, SHGFP_TYPE_CURRENT, directory))) return 3;
    if (wcslen(directory) + 90 >= MAX_PATH) return 3;
    wcscat(directory, L"\\PulseDeck"); CreateDirectoryW(directory, NULL);
    wcscat(directory, L"\\aura-installed-probe"); CreateDirectoryW(directory, NULL);
    DWORD session = 0; ProcessIdToSessionId(GetCurrentProcessId(), &session);
    swprintf(report, MAX_PATH, L"%ls\\session-%lu.json", directory, session);
    HRESULT hr = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);
    if (FAILED(hr)) { Report(report, "initialize-failed", hr); return 5; }
    hr = RegisterLocalProbe();
    const char *state = "registration-failed";
    if (SUCCEEDED(hr)) {
        ULONGLONG idle_since = GetTickCount64();
        ProbeStats previous_stats = {0};
        Report(report, "running", hr);
        for (;;) {
            if (GetFileAttributesW(stop) != INVALID_FILE_ATTRIBUTES) { state = "uninstalled"; break; }
            if (ProbeHasClients()) idle_since = GetTickCount64();
            else if (GetTickCount64() - idle_since >= 15000) {
                hr = CoSuspendClassObjects();
                if (FAILED(hr)) { state = "suspend-failed"; break; }
                if (!ProbeHasClients()) { state = "idle-exit"; break; }
                CoResumeClassObjects(); idle_since = GetTickCount64();
            }
            ProbeStats stats = GetProbeStats();
            if (memcmp(&stats, &previous_stats, sizeof(stats))) {
                Report(report, "running", S_OK); previous_stats = stats;
            }
            DWORD wait = MsgWaitForMultipleObjects(0, NULL, FALSE, 500, QS_ALLINPUT);
            if (wait == WAIT_FAILED) { hr = HRESULT_FROM_WIN32(GetLastError()); state = "wait-failed"; break; }
            MSG message;
            while (PeekMessageW(&message, NULL, 0, 0, PM_REMOVE)) {
                TranslateMessage(&message); DispatchMessageW(&message);
            }
        }
        UnregisterProbe();
    }
    CoUninitialize(); Report(report, state, hr);
    return FAILED(hr) ? 1 : 0;
}
