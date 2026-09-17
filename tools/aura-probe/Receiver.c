/* Latest-value IPC using private inherited handles; no named endpoint or network.
   The receiver never loads ASUS libraries and never accesses hardware. */
#define _WIN32_WINNT 0x0601
#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <string.h>
#include "Receiver.h"

#define RECEIVER_DEADLINE_MS 15000
#define PROBE_MAGIC 0x50444152
typedef struct ColorMessage { DWORD raw_color; BOOL synthetic; LONG sequence; } ColorMessage;
typedef struct SharedReceiver {
    DWORD magic;
    volatile LONG version;
    ColorMessage latest;
    volatile LONG acknowledged;
    ReceiverResult result;
} SharedReceiver;
struct Receiver {
    HANDLE job, process, mapping, ready, stop, changed, acknowledged;
    SharedReceiver *shared;
    CRITICAL_SECTION writer;
    LONG sequence;
};
static HANDLE parse_handle(const char *text) {
    char *end = NULL;
    unsigned long value = strtoul(text, &end, 10);
    return !*text || *end ? NULL : (HANDLE)(uintptr_t)value;
}
static BOOL snapshot(SharedReceiver *shared, ColorMessage *message) {
    for (int attempt = 0; attempt < 4; attempt++) {
        LONG before = InterlockedCompareExchange(&shared->version, 0, 0);
        if (before & 1) continue;
        *message = shared->latest;
        MemoryBarrier();
        if (before == InterlockedCompareExchange(&shared->version, 0, 0)) return TRUE;
    }
    return FALSE;
}
int RunReceiver(int argc, char **argv) {
    if (argc != 7) return 2;
    HANDLE mapping = parse_handle(argv[2]), ready = parse_handle(argv[3]), stop = parse_handle(argv[4]);
    HANDLE changed = parse_handle(argv[5]), acknowledged = parse_handle(argv[6]);
    if (!mapping || !ready || !stop || !changed || !acknowledged) return 2;
    SharedReceiver *shared = MapViewOfFile(mapping, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(*shared));
    if (!shared || shared->magic != PROBE_MAGIC) return 3;
    shared->result.process_id = GetCurrentProcessId();
    SetEvent(ready);
    ULONGLONG deadline = GetTickCount64() + RECEIVER_DEADLINE_MS;
    HANDLE waiting[] = {stop, changed};
    int code = 0;
    for (;;) {
        ULONGLONG now = GetTickCount64();
        if (now >= deadline) { code = 4; break; }
        DWORD wait = WaitForMultipleObjects(2, waiting, FALSE, (DWORD)(deadline - now));
        if (wait == WAIT_OBJECT_0) break;
        if (wait != WAIT_OBJECT_0 + 1) { code = 4; break; }
        ColorMessage message;
        if (!snapshot(shared, &message)) continue;
        if (message.sequence <= shared->acknowledged) continue;
        shared->result.has_sample = 1;
        shared->result.last_raw_color = message.raw_color;
        if (message.synthetic) shared->result.synthetic_samples++;
        else shared->result.unverified_callbacks++;
        InterlockedExchange(&shared->acknowledged, message.sequence);
        SetEvent(acknowledged);
    }
    UnmapViewOfFile(shared);
    CloseHandle(mapping); CloseHandle(ready); CloseHandle(stop);
    CloseHandle(changed); CloseHandle(acknowledged);
    return code;
}
HRESULT PublishReceiverColor(Receiver *r, DWORD raw_color, BOOL synthetic) {
    if (!r || !r->shared || !r->process || WaitForSingleObject(r->process, 0) != WAIT_TIMEOUT) return E_HANDLE;
    /* Never wait for an IPC consumer inside a HAL callback; a contending update
       may be dropped. At most one latest value exists, regardless of producer rate. */
    if (!TryEnterCriticalSection(&r->writer)) return HRESULT_FROM_WIN32(ERROR_BUSY);
    InterlockedIncrement(&r->shared->version);
    r->shared->latest.raw_color = raw_color;
    r->shared->latest.synthetic = synthetic;
    r->shared->latest.sequence = ++r->sequence;
    MemoryBarrier();
    InterlockedIncrement(&r->shared->version);
    BOOL sent = SetEvent(r->changed);
    LeaveCriticalSection(&r->writer);
    return sent ? S_OK : HRESULT_FROM_WIN32(GetLastError());
}
HRESULT TestReceiverTransport(Receiver *r) {
    /* These are transport fixtures, not calls to any Aura LED setter. */
    const DWORD patterns[] = {0x00123456, 0x00abcdef};
    for (size_t i = 0; i < sizeof(patterns)/sizeof(patterns[0]); i++) {
        HRESULT hr = PublishReceiverColor(r, patterns[i], TRUE);
        if (FAILED(hr)) return hr;
        LONG expected = r->sequence;
        ULONGLONG deadline = GetTickCount64() + 2000;
        while (InterlockedCompareExchange(&r->shared->acknowledged, 0, 0) != expected) {
            ULONGLONG now = GetTickCount64();
            if (now >= deadline || WaitForSingleObject(r->acknowledged, (DWORD)(deadline-now)) != WAIT_OBJECT_0)
                return HRESULT_FROM_WIN32(ERROR_TIMEOUT);
        }
        if (r->shared->result.last_raw_color != patterns[i] || r->shared->result.unverified_callbacks)
            return E_FAIL;
    }
    return r->shared->result.synthetic_samples == 2 ? S_OK : E_FAIL;
}
HRESULT StartReceiver(Receiver **result) {
    *result = NULL;
    Receiver *r = calloc(1, sizeof(*r));
    if (!r) return E_OUTOFMEMORY;
    InitializeCriticalSection(&r->writer);
    *result = r; /* Caller owns cleanup even on partial startup. */
    SECURITY_ATTRIBUTES security = {sizeof(security), NULL, TRUE};
    r->mapping = CreateFileMapping(INVALID_HANDLE_VALUE, &security, PAGE_READWRITE, 0, sizeof(SharedReceiver), NULL);
    if (!r->mapping) return HRESULT_FROM_WIN32(GetLastError());
    r->ready = CreateEvent(&security, TRUE, FALSE, NULL);
    if (!r->ready) return HRESULT_FROM_WIN32(GetLastError());
    r->stop = CreateEvent(&security, TRUE, FALSE, NULL);
    if (!r->stop) return HRESULT_FROM_WIN32(GetLastError());
    r->changed = CreateEvent(&security, FALSE, FALSE, NULL);
    if (!r->changed) return HRESULT_FROM_WIN32(GetLastError());
    r->acknowledged = CreateEvent(&security, FALSE, FALSE, NULL);
    if (!r->acknowledged) return HRESULT_FROM_WIN32(GetLastError());
    r->job = CreateJobObject(NULL, NULL);
    if (!r->job) return HRESULT_FROM_WIN32(GetLastError());
    r->shared = MapViewOfFile(r->mapping, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(*r->shared));
    if (!r->shared) return HRESULT_FROM_WIN32(GetLastError());
    r->shared->magic = PROBE_MAGIC;
    JOBOBJECT_EXTENDED_LIMIT_INFORMATION limits = {0};
    limits.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
    if (!SetInformationJobObject(r->job, JobObjectExtendedLimitInformation, &limits, sizeof(limits)))
        return HRESULT_FROM_WIN32(GetLastError());
    wchar_t path[MAX_PATH], command[MAX_PATH + 180];
    DWORD length = GetModuleFileNameW(NULL, path, MAX_PATH);
    if (!length || length >= MAX_PATH) return E_FAIL;
    _snwprintf(command, sizeof(command)/sizeof(command[0]), L"\"%ls\" --receiver %lu %lu %lu %lu %lu",
        path, (ULONG)(uintptr_t)r->mapping, (ULONG)(uintptr_t)r->ready, (ULONG)(uintptr_t)r->stop,
        (ULONG)(uintptr_t)r->changed, (ULONG)(uintptr_t)r->acknowledged);
    STARTUPINFOEXW startup = {0}; startup.StartupInfo.cb = sizeof(startup);
    SIZE_T bytes = 0;
    InitializeProcThreadAttributeList(NULL, 1, 0, &bytes);
    startup.lpAttributeList = malloc(bytes);
    if (!startup.lpAttributeList) return E_OUTOFMEMORY;
    BOOL attributes = InitializeProcThreadAttributeList(startup.lpAttributeList, 1, 0, &bytes);
    HANDLE handles[] = {r->mapping, r->ready, r->stop, r->changed, r->acknowledged};
    BOOL updated = attributes && UpdateProcThreadAttribute(startup.lpAttributeList, 0,
        PROC_THREAD_ATTRIBUTE_HANDLE_LIST, handles, sizeof(handles), NULL, NULL);
    PROCESS_INFORMATION process = {0};
    BOOL created = updated && CreateProcessW(path, command, NULL, NULL, TRUE,
        EXTENDED_STARTUPINFO_PRESENT | CREATE_SUSPENDED | CREATE_NO_WINDOW, NULL, NULL,
        &startup.StartupInfo, &process);
    DWORD error = GetLastError();
    if (attributes) DeleteProcThreadAttributeList(startup.lpAttributeList);
    free(startup.lpAttributeList);
    if (!created) return HRESULT_FROM_WIN32(error);
    r->process = process.hProcess;
    if (!AssignProcessToJobObject(r->job, r->process)) {
        error = GetLastError(); TerminateProcess(r->process, 1); CloseHandle(process.hThread);
        return HRESULT_FROM_WIN32(error);
    }
    DWORD resumed = ResumeThread(process.hThread);
    CloseHandle(process.hThread);
    if (resumed == (DWORD)-1) return HRESULT_FROM_WIN32(GetLastError());
    HANDLE waiting[] = {r->ready, r->process};
    if (WaitForMultipleObjects(2, waiting, FALSE, 5000) != WAIT_OBJECT_0) return E_FAIL;
    return r->shared->result.process_id && r->shared->result.process_id != GetCurrentProcessId() ? S_OK : E_FAIL;
}
HRESULT StopReceiver(Receiver *r, ReceiverResult *result) {
    if (!r) return S_OK;
    HRESULT hr = S_OK;
    if (r->stop) SetEvent(r->stop);
    if (r->process) {
        if (WaitForSingleObject(r->process, 5000) != WAIT_OBJECT_0) hr = E_FAIL;
        else {
            DWORD code = 1;
            if (!GetExitCodeProcess(r->process, &code) || code) hr = E_FAIL;
            if (r->shared) *result = r->shared->result;
        }
    }
    if (r->job) CloseHandle(r->job);
    if (r->process) { WaitForSingleObject(r->process, 5000); CloseHandle(r->process); }
    if (r->shared) UnmapViewOfFile(r->shared);
    if (r->mapping) CloseHandle(r->mapping);
    if (r->ready) CloseHandle(r->ready);
    if (r->stop) CloseHandle(r->stop);
    if (r->changed) CloseHandle(r->changed);
    if (r->acknowledged) CloseHandle(r->acknowledged);
    DeleteCriticalSection(&r->writer);
    free(r);
    return hr;
}
