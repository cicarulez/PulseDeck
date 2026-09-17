/* The SDK and our virtual HAL run only in this disposable native x86 process. */
#define COBJMACROS
#include <windows.h>
#include <oleauto.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "DiscoveryHal.h"

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

#define REQUIRE(condition) do { if (!(condition)) { \
    fprintf(stderr, "Probe assertion failed at line %d: %s\n", __LINE__, #condition); \
    goto cleanup; } } while (0)
#define CHECK(expression) REQUIRE(SUCCEEDED(expression))
#define REGCHECK(expression) REQUIRE((expression) == ERROR_SUCCESS)
#define GET(object, name, result) CHECK(invoke(object, L##name, DISPATCH_PROPERTYGET, NULL, result))
#define DISPATCH(value) REQUIRE((value).vt == VT_DISPATCH && (value).pdispVal)

int main(int argc, char **argv) {
    if (argc != 4 || !scratch_path_valid(argv[1]) ||
        (strcmp(argv[3], "empty") && strcmp(argv[3], "device"))) return 2;
    char *end = NULL;
    long iterations = strtol(argv[2], &end, 10);
    if (*end || iterations < 1 || iterations > 100) return 2;
    BOOL empty = strcmp(argv[3], "empty") == 0;
    SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX);
    HRESULT initialized = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);
    if (FAILED(initialized)) return 3;

    const GUID sdk_clsid = {0x34b707dc,0x1133,0x4ebc,{0xb3,0x80,0x21,0x38,0x7a,0x50,0xa8,0x9d}};
    const char *category = "CLSID\\{109DC3E4-B9FF-4AF3-9008-AB13705D4E5F}\\Instance\\"
        "{E9BBD754-6CF4-492E-BA89-782177A2771B}\\Instance\\{702D21B6-3A25-4D2C-9F73-F64C78E212A8}";
    IDispatch *sdk = NULL;
    HKEY root = NULL, classes = NULL, entry = NULL;
    BOOL redirected = FALSE, registered = FALSE;
    int exit_code = 1;
    VARIANT info = {0}, item = {0}, hal = {0}, devices = {0}, index = {0};
    VARIANT count = {0}, guid = {0}, device = {0}, name = {0}, width = {0}, height = {0}, lights = {0};
    index.vt = VT_I4;
    index.lVal = 0;

    CHECK(CoCreateInstance(&sdk_clsid, NULL, CLSCTX_INPROC_SERVER, &IID_IDispatch, (void**)&sdk));
    CHECK(RegisterProbe(empty));
    registered = TRUE;
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
            GET(device.pdispVal, "Width", &width);
            GET(device.pdispVal, "Height", &height);
            REQUIRE(width.vt == VT_UI4 && width.ulVal == 1 && height.vt == VT_UI4 && height.ulVal == 1);
            GET(device.pdispVal, "Lights", &lights);
            DISPATCH(lights);
            GET(lights.pdispVal, "Count", &count);
            REQUIRE(count.vt == VT_I4 && count.lVal == 1);
            VariantClear(&count); VariantClear(&lights); VariantClear(&height);
            VariantClear(&width); VariantClear(&name); VariantClear(&device);
        }
        VariantClear(&devices);
    }
    ProbeStats stats = GetProbeStats();
    REQUIRE(stats.activations == 1 && stats.enumerations >= iterations);
    REQUIRE(empty ? stats.capabilities == 0 : stats.capabilities >= iterations);
    REQUIRE(stats.effect_requests == 0 && stats.sync_requests == 0);
    exit_code = 0;

cleanup:
    if (redirected && RegOverridePredefKey(HKEY_CLASSES_ROOT, NULL)) exit_code = 1;
    VariantClear(&count); VariantClear(&lights); VariantClear(&height);
    VariantClear(&width); VariantClear(&name); VariantClear(&device);
    VariantClear(&guid); VariantClear(&devices); VariantClear(&hal);
    VariantClear(&item); VariantClear(&info);
    if (sdk) IDispatch_Release(sdk);
    if (registered && FAILED(UnregisterProbe())) exit_code = 1;
    if (entry) RegCloseKey(entry);
    if (classes) RegCloseKey(classes);
    if (root) RegCloseKey(root);
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
               "\"deviceName\":\"%s\",\"deviceCount\":%d,\"iterations\":%ld,"
               "\"halActivations\":%ld,\"halEnumerations\":%ld,\"capabilityReads\":%ld,"
               "\"effectRequests\":%ld,\"syncRequests\":%ld,"
               "\"referencesAtExit\":{\"hal\":%ld,\"device\":%ld,\"factory\":%ld}}\n",
               empty ? "" : "PulseDeck Virtual Probe", empty ? 0 : 1, iterations,
               stats.activations, stats.enumerations, stats.capabilities,
               stats.effect_requests, stats.sync_requests,
               stats.hal_refs, stats.device_refs, stats.factory_refs);
    }
    return exit_code;
}
