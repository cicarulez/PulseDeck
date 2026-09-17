/* The SDK and our virtual HAL run only in this disposable native x86 process. */
#define COBJMACROS
#include <windows.h>
#include <oleauto.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "DiscoveryHal.h"
#include "Receiver.h"
#include "IncomingSample.h"

static LONG WINAPI report_probe_crash(EXCEPTION_POINTERS *exception) {
    HMODULE module = NULL;
    char path[MAX_PATH] = {0};
    void *address = exception->ExceptionRecord->ExceptionAddress;
    GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
        (LPCSTR)address, &module);
    if (module) GetModuleFileNameA(module, path, MAX_PATH);
    const char *name = strrchr(path, '\\');
    fprintf(stderr, "Probe crash: exception=0x%08lx module=%s offset=0x%lx\n",
        exception->ExceptionRecord->ExceptionCode, name ? name + 1 : path,
        (unsigned long)((ULONG_PTR)address - (ULONG_PTR)module));
    fflush(stderr);
    return EXCEPTION_EXECUTE_HANDLER;
}

static HRESULT invoke(IDispatch *object, wchar_t *name, WORD flags,
                      VARIANT *argument, VARIANT *result) {
    DISPID id;
    HRESULT hr = IDispatch_GetIDsOfNames(object, &IID_NULL, &name, 1,
                                        LOCALE_USER_DEFAULT, &id);
    if (FAILED(hr)) return hr;
    DISPPARAMS args = {argument, NULL, argument ? 1 : 0, 0};
    EXCEPINFO exception = {0};
    UINT argument_error = 0;
    VariantInit(result);
    hr = IDispatch_Invoke(object, id, &IID_NULL, LOCALE_USER_DEFAULT, flags,
                          &args, result, &exception, &argument_error);
    if (exception.pfnDeferredFillIn) exception.pfnDeferredFillIn(&exception);
    SysFreeString(exception.bstrSource);
    SysFreeString(exception.bstrDescription);
    SysFreeString(exception.bstrHelpFile);
    if (FAILED(hr)) fprintf(stderr, "%ls failed: 0x%08lx\n", name, hr);
    return hr;
}

static BOOL scratch_path_valid(const char *path) {
    const char *prefix = "Software\\PulseDeck\\AuraProbe\\";
    size_t length = strlen(prefix);
    if (strncmp(path, prefix, length) || strlen(path + length) != 32) return FALSE;
    return strspn(path + length, "0123456789abcdef") == 32;
}

/* Read the running service's inventory only. The supervisor checks it is already
   running and bounds this disposable process. Never call profile/engine setters. */
static int read_service_metadata(BOOL devices) {
    const GUID mediator_clsid = {0x95775dc4,0x77aa,0x4e94,{0x8c,0xf6,0x68,0x26,0x7e,0xef,0x18,0x56}};
    IDispatch *service = NULL;
    VARIANT result = {0};
    char *utf8 = NULL;
    int exit_code = 1;
    SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX);
    HRESULT hr = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);
    if (FAILED(hr)) return 3;
    hr = CoCreateInstance(&mediator_clsid, NULL, CLSCTX_LOCAL_SERVER, &IID_IDispatch, (void**)&service);
    if (SUCCEEDED(hr)) hr = invoke(service, devices ? L"get_QueryAllDevice" : L"get_QueryAllDeviceCap",
                                  DISPATCH_METHOD, NULL, &result);
    if (SUCCEEDED(hr) && result.vt == VT_BSTR && result.bstrVal) {
        UINT length = SysStringLen(result.bstrVal);
        if (length && length <= 1024 * 1024) {
            int bytes = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, result.bstrVal,
                                            (int)length, NULL, 0, NULL, NULL);
            utf8 = bytes > 0 ? malloc((size_t)bytes) : NULL;
            if (utf8 && WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, result.bstrVal,
                    (int)length, utf8, bytes, NULL, NULL) == bytes &&
                    fwrite(utf8, 1, (size_t)bytes, stdout) == (size_t)bytes) exit_code = 0;
        }
    }
    if (exit_code) fprintf(stderr, "Service capability read failed: 0x%08lx\n", hr);
    free(utf8);
    VariantClear(&result);
    if (service) IDispatch_Release(service);
    CoUninitialize();
    return exit_code;
}

#define REQUIRE(condition) do { if (!(condition)) { \
    fprintf(stderr, "Probe assertion failed at line %d: %s\n", __LINE__, #condition); \
    goto cleanup; } } while (0)
#define CHECK(expression) REQUIRE(SUCCEEDED(expression))
#define REGCHECK(expression) REQUIRE((expression) == ERROR_SUCCESS)
#define GET(object, name, result) CHECK(invoke(object, L##name, DISPATCH_PROPERTYGET, NULL, result))
#define DISPATCH(value) REQUIRE((value).vt == VT_DISPATCH && (value).pdispVal)

/* Enumerate registration metadata only; never call CreateHal on real entries. */
static int read_registered_hal_info(void) {
    const GUID sdk_clsid = {0x34b707dc,0x1133,0x4ebc,{0xb3,0x80,0x21,0x38,0x7a,0x50,0xa8,0x9d}};
    HRESULT initialized = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);
    if (FAILED(initialized)) return 3;
    IDispatch *sdk = NULL;
    VARIANT collection = {0}, count = {0}, item = {0}, guid = {0}, index = {0};
    LONG total = 0, matches = 0;
    int exit_code = 1;
    CHECK(CoCreateInstance(&sdk_clsid, NULL, CLSCTX_INPROC_SERVER, &IID_IDispatch, (void**)&sdk));
    CHECK(invoke(sdk, L"EumerateHalInfo", DISPATCH_METHOD, NULL, &collection));
    DISPATCH(collection);
    GET(collection.pdispVal, "Count", &count);
    REQUIRE(count.vt == VT_I4 && count.lVal >= 0 && count.lVal <= 1024);
    total = count.lVal; index.vt = VT_I4;
    for (index.lVal = 0; index.lVal < total; index.lVal++) {
        CHECK(invoke(collection.pdispVal, L"Item", DISPATCH_PROPERTYGET, &index, &item));
        DISPATCH(item);
        GET(item.pdispVal, "Guid", &guid);
        REQUIRE(guid.vt == VT_BSTR && guid.bstrVal);
        if (!_wcsicmp(guid.bstrVal, PROBE_GUID_TEXT)) matches++;
        VariantClear(&guid); VariantClear(&item);
    }
    REQUIRE(matches <= 1);
    printf("{\"scope\":\"registered HAL metadata only; no activation\","
           "\"registeredHalCount\":%ld,\"probeMatches\":%ld}\n", total, matches);
    exit_code = 0;
cleanup:
    VariantClear(&guid); VariantClear(&item); VariantClear(&count); VariantClear(&collection);
    if (sdk) IDispatch_Release(sdk);
    CoUninitialize();
    return exit_code;
}

int main(int argc, char **argv) {
    SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX);
    SetUnhandledExceptionFilter(report_probe_crash);
    if (argc == 2 && !strcmp(argv[1], "--registered-hal-info")) return read_registered_hal_info();
    if ((argc == 4 || argc == 5) && !strcmp(argv[1], "--com-server"))
        return RunComServer(argv[2], argv[3], argc == 5 ? argv[4] : "15");
    if (argc == 2 && !strcmp(argv[1], "--remote-contracts")) return CheckRemoteContracts();
    if (argc == 2 && !strcmp(argv[1], "--remote-absent")) return CheckRemoteAbsent();
    if (argc == 2 && !strcmp(argv[1], "--service-capabilities")) return read_service_metadata(FALSE);
    if (argc == 2 && !strcmp(argv[1], "--service-devices")) return read_service_metadata(TRUE);
    if (argc > 1 && !strcmp(argv[1], "--receiver")) return RunReceiver(argc, argv);
    if (argc == 2 && !strcmp(argv[1], "--check-contracts")) return CheckProbeContracts();
    if (argc == 2 && !strcmp(argv[1], "--check-incoming")) return CheckIncomingWordDecoder();
    BOOL separate = (argc == 5 || argc == 6) && (!strcmp(argv[4], "separate") || !strcmp(argv[4], "transport-test"));
    BOOL transport_test = separate && !strcmp(argv[4], "transport-test");
    if ((argc != 4 && !separate) || !scratch_path_valid(argv[1]) ||
        (strcmp(argv[3], "empty") && strcmp(argv[3], "device") && strcmp(argv[3], "remote"))) return 2;
    char *end = NULL;
    long iterations = strtol(argv[2], &end, 10);
    if (*end || iterations < 1 || iterations > 100) return 2;
    long observe_seconds = argc == 6 ? strtol(argv[5], &end, 10) : 0;
    if (*end || observe_seconds < 0 || observe_seconds > 10) return 2;
    BOOL empty = strcmp(argv[3], "empty") == 0;
    BOOL remote = strcmp(argv[3], "remote") == 0;
    SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX);
    HRESULT initialized = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);
    if (FAILED(initialized)) return 3;

    const GUID sdk_clsid = {0x34b707dc,0x1133,0x4ebc,{0xb3,0x80,0x21,0x38,0x7a,0x50,0xa8,0x9d}};
    const char *category = "CLSID\\{109DC3E4-B9FF-4AF3-9008-AB13705D4E5F}\\Instance\\"
        "{E9BBD754-6CF4-492E-BA89-782177A2771B}\\Instance\\{702D21B6-3A25-4D2C-9F73-F64C78E212A8}";
    IDispatch *sdk = NULL;
    Receiver *receiver = NULL;
    ReceiverResult receiver_result = {0};
    HKEY root = NULL, classes = NULL, entry = NULL;
    BOOL redirected = FALSE, registered = FALSE;
    int exit_code = 1;
    VARIANT info = {0}, item = {0}, hal = {0}, devices = {0}, index = {0};
    VARIANT count = {0}, guid = {0}, device = {0}, name = {0}, width = {0}, height = {0}, lights = {0};
    VARIANT effects = {0}, effect = {0}, effect_name = {0}, effect_id = {0}, synchronized = {0}, device_type = {0};
    index.vt = VT_I4;
    index.lVal = 0;

    CHECK(CoCreateInstance(&sdk_clsid, NULL, CLSCTX_INPROC_SERVER, &IID_IDispatch, (void**)&sdk));
    if (separate) CHECK(StartReceiver(&receiver));
    if (transport_test) CHECK(TestReceiverTransport(receiver));
    SetProbeReceiver(receiver);
    if (!remote) {
        CHECK(RegisterProbe(empty));
        registered = TRUE;
    }
    REGCHECK(RegOpenKeyExA(HKEY_CURRENT_USER, argv[1], 0, KEY_ALL_ACCESS, &root));
    REGCHECK(RegCreateKeyExA(root, "Classes", 0, NULL, 0, KEY_ALL_ACCESS, NULL, &classes, NULL));
    REGCHECK(RegCreateKeyExA(classes, category, 0, NULL, 0, KEY_ALL_ACCESS, NULL, &entry, NULL));
    DWORD plug = 0;
    const char label[] = "PulseDeck Virtual Probe";
    REGCHECK(RegSetValueExA(entry, "Name", 0, REG_SZ, (BYTE*)label, sizeof(label)));
    REGCHECK(RegSetValueExA(entry, "Pluging", 0, REG_DWORD, (BYTE*)&plug, sizeof(plug)));
    REGCHECK(RegOverridePredefKey(HKEY_CLASSES_ROOT, classes));
    redirected = TRUE;
    CHECK(invoke(sdk, L"EumerateHalInfo", DISPATCH_METHOD, NULL, &info));
    REGCHECK(RegOverridePredefKey(HKEY_CLASSES_ROOT, NULL));
    redirected = FALSE;
    DISPATCH(info);
    GET(info.pdispVal, "Count", &count);
    REQUIRE(count.vt == VT_I4 && count.lVal == 1);
    VariantClear(&count);
    CHECK(invoke(info.pdispVal, L"Item", DISPATCH_PROPERTYGET, &index, &item));
    DISPATCH(item);
    GET(item.pdispVal, "Guid", &guid);
    REQUIRE(guid.vt == VT_BSTR && guid.bstrVal && !_wcsicmp(guid.bstrVal, PROBE_GUID_TEXT));
    CHECK(invoke(item.pdispVal, L"CreateHal", DISPATCH_METHOD, NULL, &hal));
    DISPATCH(hal);

    for (long iteration = 0; iteration < iterations; iteration++) {
        CHECK(invoke(hal.pdispVal, L"EumerateDevices", DISPATCH_METHOD, NULL, &devices));
        DISPATCH(devices);
        GET(devices.pdispVal, "Count", &count);
        REQUIRE(count.vt == VT_I4 && count.lVal == (empty ? 0 : 1));
        VariantClear(&count);
        if (!empty) {
            CHECK(invoke(devices.pdispVal, L"Item", DISPATCH_PROPERTYGET, &index, &device));
            DISPATCH(device);
            GET(device.pdispVal, "Name", &name);
            REQUIRE(name.vt == VT_BSTR && name.bstrVal && !wcscmp(name.bstrVal, PROBE_DEVICE_NAME));
            GET(device.pdispVal, "Type", &device_type);
            REQUIRE(device_type.vt == VT_UI4 && device_type.ulVal == PROBE_DEVICE_TYPE);
            VariantClear(&device_type);
            GET(device.pdispVal, "Width", &width);
            GET(device.pdispVal, "Height", &height);
            REQUIRE(width.vt == VT_UI4 && width.ulVal == 1 && height.vt == VT_UI4 && height.ulVal == 1);
            GET(device.pdispVal, "Lights", &lights);
            DISPATCH(lights);
            GET(lights.pdispVal, "Count", &count);
            REQUIRE(count.vt == VT_I4 && count.lVal == 1);
            VariantClear(&count);
            GET(device.pdispVal, "Effects", &effects);
            DISPATCH(effects);
            GET(effects.pdispVal, "Count", &count);
            REQUIRE(count.vt == VT_I4 && count.lVal == 1);
            CHECK(invoke(effects.pdispVal, L"Item", DISPATCH_PROPERTYGET, &index, &effect));
            DISPATCH(effect);
            GET(effect.pdispVal, "Name", &effect_name);
            GET(effect.pdispVal, "Id", &effect_id);
            GET(effect.pdispVal, "Synchronized", &synchronized);
            REQUIRE(effect_name.vt == VT_BSTR && effect_name.bstrVal && !wcscmp(effect_name.bstrVal, L"Static"));
            REQUIRE(effect_id.vt == VT_I4 && effect_id.lVal == 1);
            REQUIRE(synchronized.vt == VT_I4 && synchronized.lVal == 0);
            VariantClear(&synchronized); VariantClear(&effect_id); VariantClear(&effect_name);
            VariantClear(&effect); VariantClear(&effects);
            VariantClear(&count); VariantClear(&lights); VariantClear(&height);
            VariantClear(&width); VariantClear(&name); VariantClear(&device);
        }
        VariantClear(&devices);
    }
    ProbeStats stats = GetProbeStats();
    if (!remote) {
        REQUIRE(stats.activations == 1 && stats.enumerations >= iterations);
        REQUIRE(empty ? stats.capabilities == 0 : stats.capabilities >= iterations);
    } else REQUIRE(stats.activations == 0 && stats.enumerations == 0 && stats.capabilities == 0);
    REQUIRE(stats.effect_requests == 0 && stats.sync_requests == 0);
    if (observe_seconds) Sleep((DWORD)observe_seconds * 1000);
    exit_code = 0;

cleanup:
    if (redirected && RegOverridePredefKey(HKEY_CLASSES_ROOT, NULL)) exit_code = 1;
    VariantClear(&device_type);
    VariantClear(&synchronized); VariantClear(&effect_id); VariantClear(&effect_name);
    VariantClear(&effect); VariantClear(&effects);
    VariantClear(&count); VariantClear(&lights); VariantClear(&height);
    VariantClear(&width); VariantClear(&name); VariantClear(&device);
    VariantClear(&guid); VariantClear(&devices); VariantClear(&hal);
    VariantClear(&item); VariantClear(&info);
    if (sdk) IDispatch_Release(sdk);
    if (registered && FAILED(UnregisterProbe())) exit_code = 1;
    if (entry) RegCloseKey(entry);
    if (classes) RegCloseKey(classes);
    if (root) RegCloseKey(root);
    SetProbeReceiver(NULL);
    if (receiver && FAILED(StopReceiver(receiver, &receiver_result))) exit_code = 1;
    if (separate && (receiver_result.unverified_callbacks ||
        receiver_result.synthetic_samples != (transport_test ? 2 : 0) ||
        receiver_result.has_sample != (transport_test ? 1 : 0))) exit_code = 1;
    CoUninitialize();
    stats = GetProbeStats();
    /* SDK references may survive releasing its returned collections. Do not
       compensate with extra Release calls: report them and end the process. */
    if (stats.hal_refs < 1 || stats.device_refs < 1 || stats.factory_refs != 1) {
        fprintf(stderr, "Invalid COM lifetime counters.\n");
        exit_code = 1;
    }
    if (!exit_code) {
        printf("{\"scope\":\"isolated SDK; not Armoury Crate\",\"detected\":true,"
               "\"externalComHal\":%s,"
               "\"receiverProcessId\":%lu,\"syntheticSamples\":%ld,\"unverifiedCallbacks\":%ld,"
               "\"hasColorSample\":%s,\"colorSource\":\"%s\",\"deviceName\":\"%s\",\"deviceCount\":%d,\"iterations\":%ld,"
               "\"halActivations\":%ld,\"halEnumerations\":%ld,\"capabilityReads\":%ld,"
               "\"staticEffectDescriptorVerified\":%s,"
               "\"effectRequests\":%ld,\"syncRequests\":%ld,"
               "\"referencesAtExit\":{\"hal\":%ld,\"device\":%ld,\"factory\":%ld}}\n",
               remote ? "true" : "false", receiver_result.process_id, receiver_result.synthetic_samples, receiver_result.unverified_callbacks,
               receiver_result.has_sample ? "true" : "false",
               receiver_result.has_sample ? "synthetic-test" : "unavailable",
               empty ? "" : "PulseDeck Virtual Probe", empty ? 0 : 1, iterations,
               stats.activations, stats.enumerations, stats.capabilities,
               empty ? "false" : "true",
               stats.effect_requests, stats.sync_requests,
               stats.hal_refs, stats.device_refs, stats.factory_refs);
    }
    return exit_code;
}
