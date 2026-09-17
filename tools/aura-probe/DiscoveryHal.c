/* Experimental, process-local COM HAL. No hardware or network access. */
#define COBJMACROS
#include <windows.h>
#include <oleauto.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "DiscoveryHal.h"

static const GUID probe_clsid = {0x702d21b6,0x3a25,0x4d2c,{0x9f,0x73,0xf6,0x4c,0x78,0xe2,0x12,0xa8}};
static const GUID hal_iid = {0xf2c8d5b4,0x3854,0x4325,{0x8a,0x4f,0xfd,0x7c,0x50,0x72,0xe3,0xb9}};
static LONG activations, enumerations, capabilities, effect_requests, sync_requests;
static LONG legacy_enumerations, array_enumerations;
static LONG factory_refs = 1;
static BOOL empty_devices;
static DWORD cookie;
static Receiver *receiver;
void SetProbeReceiver(Receiver *target) { receiver = target; }
static const GUID device_iid = {0x61711778,0xab59,0x4026,{0x89,0xe8,0x7a,0x63,0x42,0x2c,0x29,0xc2}};
static const GUID device_opt_iid = {0x68f0c6e1,0x7469,0x40b3,{0x84,0xd5,0xe0,0x79,0x3f,0x44,0x9e,0x4d}};
static const GUID device2_iid = {0xc40349d9,0xd85a,0x477a,{0x8a,0x52,0x65,0xf3,0x2c,0x0b,0x5d,0x2f}};
static const GUID device_opt2_iid = {0xa146f057,0x41d4,0x4358,{0x8a,0xeb,0x57,0xd3,0x4d,0x2c,0x59,0x43}};
typedef struct Device Device;
typedef struct DeviceVtbl {
    HRESULT (STDMETHODCALLTYPE *QueryInterface)(Device*, REFIID, void**);
    ULONG (STDMETHODCALLTYPE *AddRef)(Device*);
    ULONG (STDMETHODCALLTYPE *Release)(Device*);
    HRESULT (STDMETHODCALLTYPE *GetCapability)(Device*, BSTR*);
    HRESULT (STDMETHODCALLTYPE *SetEffect)(Device*, ULONG, ULONG*, ULONG);
    HRESULT (STDMETHODCALLTYPE *Synchronize)(Device*, ULONG, ULONGLONG);
    HRESULT (STDMETHODCALLTYPE *SetEffectOptSpeed)(Device*, ULONG, ULONG*, ULONG, ULONG, ULONG);
    HRESULT (STDMETHODCALLTYPE *SetEffect2)(Device*, ULONG, VARIANT, ULONG);
    HRESULT (STDMETHODCALLTYPE *SetEffectOptSpeed2)(Device*, ULONG, VARIANT, ULONG, ULONG, ULONG);
} DeviceVtbl;
struct Device { const DeviceVtbl *lpVtbl; LONG refs; };
static ULONG STDMETHODCALLTYPE device_add(Device *self) { return InterlockedIncrement(&self->refs); }
static ULONG STDMETHODCALLTYPE device_release(Device *self) { return InterlockedDecrement(&self->refs); }
static HRESULT STDMETHODCALLTYPE device_query(Device *self, REFIID iid, void **result) {
    if (!result) return E_POINTER;
    *result = NULL;
    if (!IsEqualGUID(iid, &IID_IUnknown) && !IsEqualGUID(iid, &device_iid) &&
        !IsEqualGUID(iid, &device_opt_iid) && !IsEqualGUID(iid, &device2_iid) &&
        !IsEqualGUID(iid, &device_opt2_iid)) return E_NOINTERFACE;
    *result = self; device_add(self); return S_OK;
}
static HRESULT STDMETHODCALLTYPE device_capability(Device *self, BSTR *xml) {
    (void)self;
    if (!xml) return E_POINTER;
    *xml = SysAllocString(L"<capability><version>1</version><type>0</type>"
        L"<device><name>" PROBE_DEVICE_NAME L"</name><id>0</id><manufacturer>PulseDeck</manufacturer>"
        L"<model>Isolated test destination</model><layout><led_count>1</led_count>"
        L"<size><width>1</width><height>1</height></size></layout>"
        L"<supported_effect><effect><name>Static</name><id>1</id>"
        L"<synchronizable>0</synchronizable></effect></supported_effect>"
        L"</device></capability>");
    InterlockedIncrement(&capabilities);
    return *xml ? S_OK : E_OUTOFMEMORY;
}
static HRESULT STDMETHODCALLTYPE device_effect(Device *self, ULONG effect, ULONG *colors, ULONG count) {
    (void)self;
    InterlockedIncrement(&effect_requests);
    /* This is an incoming HAL callback, never invoked by our probe. Keep the
       packed word unverified until its effect/color contract is validated. */
    if (effect != 1) return E_NOTIMPL;
    if (!colors || count != 1) return E_INVALIDARG;
    return receiver ? PublishReceiverColor(receiver, colors[0], FALSE) : E_NOTIMPL;
}
static HRESULT STDMETHODCALLTYPE device_sync(Device *self, ULONG effect, ULONGLONG time) {
    (void)self; (void)effect; (void)time;
    InterlockedIncrement(&sync_requests);
    return E_NOTIMPL;
}
/* The SDK's out-of-process enumeration requires Opt2 even for metadata reads.
   These additional incoming callbacks are deliberately unsupported in this probe;
   do not claim color reception before their payload contract has been tested. */
static HRESULT STDMETHODCALLTYPE device_effect_opt(Device *self, ULONG effect, ULONG *colors,
                                                   ULONG count, ULONG speed, ULONG direction) {
    (void)self; (void)effect; (void)colors; (void)count; (void)speed; (void)direction;
    InterlockedIncrement(&effect_requests); return E_NOTIMPL;
}
static HRESULT STDMETHODCALLTYPE device_effect2(Device *self, ULONG effect, VARIANT colors, ULONG count) {
    (void)self; (void)effect; (void)colors; (void)count;
    InterlockedIncrement(&effect_requests); return E_NOTIMPL;
}
static HRESULT STDMETHODCALLTYPE device_effect_opt2(Device *self, ULONG effect, VARIANT colors,
                                                    ULONG count, ULONG speed, ULONG direction) {
    (void)self; (void)effect; (void)colors; (void)count; (void)speed; (void)direction;
    InterlockedIncrement(&effect_requests); return E_NOTIMPL;
}
static const DeviceVtbl device_vtbl={device_query,device_add,device_release,device_capability,
    device_effect,device_sync,device_effect_opt,device_effect2,device_effect_opt2};
static Device device={&device_vtbl,1};
typedef struct Hal Hal;
typedef struct HalVtbl {
    HRESULT (STDMETHODCALLTYPE *QueryInterface)(Hal*, REFIID, void**);
    ULONG (STDMETHODCALLTYPE *AddRef)(Hal*);
    ULONG (STDMETHODCALLTYPE *Release)(Hal*);
    HRESULT (STDMETHODCALLTYPE *Enumerate)(Hal*, IUnknown**, ULONG*);
    HRESULT (STDMETHODCALLTYPE *Enumerate2)(Hal*, VARIANT*, ULONG*);
} HalVtbl;
struct Hal { const HalVtbl *lpVtbl; LONG refs; };
static ULONG STDMETHODCALLTYPE hal_add(Hal *self) { return InterlockedIncrement(&self->refs); }
static ULONG STDMETHODCALLTYPE hal_release(Hal *self) { return InterlockedDecrement(&self->refs); }
static HRESULT STDMETHODCALLTYPE hal_query(Hal *self, REFIID iid, void **result) {
    if (!result) return E_POINTER;
    *result = NULL;
    if (!IsEqualGUID(iid, &IID_IUnknown) && !IsEqualGUID(iid, &hal_iid)) return E_NOINTERFACE;
    *result = self; hal_add(self); return S_OK;
}
static HRESULT STDMETHODCALLTYPE hal_enumerate(Hal *self, IUnknown **devices, ULONG *count) {
    (void)self;
    InterlockedIncrement(&enumerations);
    InterlockedIncrement(&legacy_enumerations);
    if (!count) return E_POINTER;
    ULONG capacity=*count;
    *count = empty_devices ? 0 : 1;
    if (empty_devices) return S_OK;
    if (devices && capacity >= 1) { devices[0]=(IUnknown*)&device; device_add(&device); }
    return devices && capacity < 1 ? E_INVALIDARG : S_OK;
}
static HRESULT STDMETHODCALLTYPE hal_enumerate2(Hal *self, VARIANT *devices, ULONG *count) {
    (void)self;
    InterlockedIncrement(&enumerations);
    InterlockedIncrement(&array_enumerations);
    if (!count) return E_POINTER;
    *count = empty_devices ? 0 : 1;
    if (!devices) return S_OK;
    VariantInit(devices);
    SAFEARRAY *array=SafeArrayCreateVector(VT_UNKNOWN,0,*count);
    if(!array)return E_OUTOFMEMORY;
    LONG index=0;
    HRESULT hr=empty_devices ? S_OK : SafeArrayPutElement(array,&index,(IUnknown*)&device);
    if(FAILED(hr)){SafeArrayDestroy(array);return hr;}
    devices->vt=VT_ARRAY|VT_UNKNOWN;devices->parray=array;return S_OK;
}
static const HalVtbl hal_vtbl = {hal_query, hal_add, hal_release, hal_enumerate, hal_enumerate2};
/* Root reference keeps the test singleton alive until process exit. */
static Hal hal = {&hal_vtbl, 1};
static HRESULT STDMETHODCALLTYPE factory_query(IClassFactory *self, REFIID iid, void **result) {
    if (!result) return E_POINTER;
    *result = NULL;
    if (!IsEqualGUID(iid, &IID_IUnknown) && !IsEqualGUID(iid, &IID_IClassFactory)) return E_NOINTERFACE;
    *result = self; InterlockedIncrement(&factory_refs); return S_OK;
}
static ULONG STDMETHODCALLTYPE factory_add(IClassFactory *self) { (void)self; return InterlockedIncrement(&factory_refs); }
static ULONG STDMETHODCALLTYPE factory_release(IClassFactory *self) { (void)self; return InterlockedDecrement(&factory_refs); }
static HRESULT STDMETHODCALLTYPE factory_create(IClassFactory *self, IUnknown *outer, REFIID iid, void **result) {
    (void)self;
    if (!result) return E_POINTER;
    *result = NULL;
    if (outer) return CLASS_E_NOAGGREGATION;
    InterlockedIncrement(&activations);
    return hal_query(&hal, iid, result);
}
static HRESULT STDMETHODCALLTYPE factory_lock(IClassFactory *self, BOOL lock) { (void)self; (void)lock; return S_OK; }
static IClassFactoryVtbl factory_vtbl = {factory_query, factory_add, factory_release, factory_create, factory_lock};
static IClassFactory factory = {&factory_vtbl};
HRESULT RegisterProbe(BOOL empty) {
    if (cookie) return E_UNEXPECTED;
    empty_devices = empty;
    return CoRegisterClassObject(&probe_clsid, (IUnknown*)&factory, CLSCTX_INPROC_SERVER, REGCLS_MULTIPLEUSE, &cookie);
}
HRESULT UnregisterProbe(void) {
    if (!cookie) return S_FALSE;
    HRESULT hr = CoRevokeClassObject(cookie); cookie = 0; return hr;
}
ProbeStats GetProbeStats(void) {
    ProbeStats stats = {activations, enumerations, capabilities, effect_requests,
        sync_requests, hal.refs, device.refs, factory_refs};
    return stats;
}

/* Temporary running EXE registration only: no Classes/ASUS registry changes.
   A message pump is required because the factory and objects belong to this STA. */
int RunComServer(const char *parent_pid, const char *event_suffix, const char *lifetime) {
    char *end = NULL;
    DWORD pid = strtoul(parent_pid, &end, 10);
    if (!pid || *end || strlen(event_suffix) != 32 ||
        strspn(event_suffix, "0123456789abcdef") != 32) return 2;
    unsigned long seconds = strtoul(lifetime, &end, 10);
    if (!*lifetime || *end || seconds < 1 || seconds > 120) return 2;
    char event_name[100];
    snprintf(event_name, sizeof(event_name), "Local\\PulseDeck.AuraProbe.Ready.%s", event_suffix);
    HANDLE ready = OpenEventA(EVENT_MODIFY_STATE, FALSE, event_name);
    snprintf(event_name, sizeof(event_name), "Local\\PulseDeck.AuraProbe.Stop.%s", event_suffix);
    HANDLE stop = OpenEventA(SYNCHRONIZE, FALSE, event_name);
    HANDLE parent = OpenProcess(SYNCHRONIZE, FALSE, pid);
    if (!ready || !stop || !parent) {
        if (ready) CloseHandle(ready);
        if (stop) CloseHandle(stop);
        if (parent) CloseHandle(parent);
        return 3;
    }
    HRESULT hr = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);
    BOOL initialized = SUCCEEDED(hr);
    if (initialized) hr = CoRegisterClassObject(&probe_clsid, (IUnknown*)&factory,
        CLSCTX_LOCAL_SERVER, REGCLS_MULTIPLEUSE, &cookie);
    int code = 1;
    if (SUCCEEDED(hr) && SetEvent(ready)) {
        ULONGLONG deadline = GetTickCount64() + seconds * 1000;
        HANDLE waiting[] = {stop, parent};
        for (;;) {
            ULONGLONG now = GetTickCount64();
            if (now >= deadline) break;
            DWORD wait = MsgWaitForMultipleObjects(2, waiting, FALSE,
                (DWORD)(deadline - now), QS_ALLINPUT);
            if (wait == WAIT_OBJECT_0 || wait == WAIT_OBJECT_0 + 1) { code = 0; break; }
            if (wait != WAIT_OBJECT_0 + 2) break;
            MSG message;
            while (PeekMessage(&message, NULL, 0, 0, PM_REMOVE)) {
                TranslateMessage(&message); DispatchMessage(&message);
            }
        }
    } else fprintf(stderr, "Local server registration failed: 0x%08lx\n", hr);
    if (cookie && FAILED(UnregisterProbe())) code = 1;
    if (initialized) CoUninitialize();
    CloseHandle(ready); CloseHandle(stop); CloseHandle(parent);
    ProbeStats s = GetProbeStats();
    if (s.effect_requests || s.sync_requests) code = 1;
    printf("{\"scope\":\"temporary COM server; registration owned by supervisor\","
           "\"processId\":%lu,\"activations\":%ld,\"enumerations\":%ld,\"capabilities\":%ld,"
           "\"legacyEnumerations\":%ld,\"arrayEnumerations\":%ld,"
           "\"effectRequests\":%ld,\"syncRequests\":%ld}\n", GetCurrentProcessId(),
           s.activations, s.enumerations, s.capabilities, legacy_enumerations, array_enumerations,
           s.effect_requests, s.sync_requests);
    return code;
}

int CheckRemoteContracts(void) {
    HRESULT hr = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);
    if (FAILED(hr)) return 3;
    Hal *remote = NULL;
    Device *remote_device = NULL;
    IUnknown *item = NULL;
    VARIANT devices = {0};
    ULONG count = 0;
    BSTR xml = NULL;
    const char *step = "activate";
    hr = CoCreateInstance(&probe_clsid, NULL, CLSCTX_LOCAL_SERVER, &hal_iid, (void**)&remote);
    if (FAILED(hr)) goto cleanup;
    step = "Enumerate2";
    hr = remote->lpVtbl->Enumerate2(remote, &devices, &count);
    if (FAILED(hr)) goto cleanup;
    if (count != 1 || devices.vt != (VT_ARRAY | VT_UNKNOWN)) { hr = E_UNEXPECTED; goto cleanup; }
    LONG index = 0;
    step = "array element";
    hr = SafeArrayGetElement(devices.parray, &index, &item);
    if (FAILED(hr) || !item) { hr = E_UNEXPECTED; goto cleanup; }
    step = "device interface";
    hr = IUnknown_QueryInterface(item, &device_opt2_iid, (void**)&remote_device);
    if (FAILED(hr)) goto cleanup;
    step = "GetCapability";
    hr = remote_device->lpVtbl->GetCapability(remote_device, &xml);
    if (FAILED(hr)) goto cleanup;
    if (!xml || !wcsstr(xml, PROBE_DEVICE_NAME)) hr = E_UNEXPECTED;
cleanup:
    printf("{\"scope\":\"cross-process COM contracts only\",\"step\":\"%s\","
           "\"hresult\":\"0x%08lx\",\"deviceCount\":%lu,\"variantType\":%u}\n",
           step, hr, count, devices.vt);
    SysFreeString(xml);
    if (remote_device) remote_device->lpVtbl->Release(remote_device);
    if (item) IUnknown_Release(item);
    VariantClear(&devices);
    if (remote) remote->lpVtbl->Release(remote);
    CoUninitialize();
    return FAILED(hr) ? 1 : 0;
}

int CheckRemoteAbsent(void) {
    HRESULT hr = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);
    if (FAILED(hr)) return 3;
    IClassFactory *remote = NULL;
    hr = CoGetClassObject(&probe_clsid, CLSCTX_LOCAL_SERVER, NULL, &IID_IClassFactory, (void**)&remote);
    if (remote) IClassFactory_Release(remote);
    CoUninitialize();
    printf("{\"scope\":\"COM registration cleanup\",\"classAbsent\":%s,\"hresult\":\"0x%08lx\"}\n",
        hr == REGDB_E_CLASSNOTREG ? "true" : "false", hr);
    return hr == REGDB_E_CLASSNOTREG ? 0 : 1;
}

/* Exercise both interface-return paths with a caller that releases every returned
   reference. No ASUS SDK, service, effect or synchronization method is invoked. */
int CheckProbeContracts(void) {
    for (int iteration = 0; iteration < 100; iteration++) {
        void *instance = NULL;
        if (FAILED(factory_create(&factory, NULL, &hal_iid, &instance))) return 1;
        Hal *test_hal = instance;
        ULONG count = 0;
        if (FAILED(test_hal->lpVtbl->Enumerate(test_hal, NULL, &count)) || count != 1) return 1;
        IUnknown *item = NULL;
        if (FAILED(test_hal->lpVtbl->Enumerate(test_hal, &item, &count)) || !item) return 1;
        Device *test_device = NULL;
        if (FAILED(IUnknown_QueryInterface(item, &device_iid, (void**)&test_device))) return 1;
        /* All inherited interface views must preserve canonical IUnknown identity. */
        const GUID *interfaces[] = {&device_opt_iid, &device2_iid, &device_opt2_iid};
        for (size_t i = 0; i < sizeof(interfaces)/sizeof(interfaces[0]); i++) {
            IUnknown *view = NULL, *identity = NULL;
            if (FAILED(IUnknown_QueryInterface(item, interfaces[i], (void**)&view))) return 1;
            if (FAILED(IUnknown_QueryInterface(view, &IID_IUnknown, (void**)&identity))) return 1;
            if (identity != item) return 1;
            IUnknown_Release(identity); IUnknown_Release(view);
        }
        BSTR xml = NULL;
        if (FAILED(test_device->lpVtbl->GetCapability(test_device, &xml)) || !xml) return 1;
        SysFreeString(xml);
        test_device->lpVtbl->Release(test_device);
        IUnknown_Release(item);
        VARIANT array = {0};
        if (FAILED(test_hal->lpVtbl->Enumerate2(test_hal, &array, &count)) ||
            array.vt != (VT_ARRAY | VT_UNKNOWN) || count != 1) return 1;
        LONG index = 0;
        item = NULL;
        if (FAILED(SafeArrayGetElement(array.parray, &index, &item)) || !item) return 1;
        IUnknown_Release(item);
        VariantClear(&array);
        test_hal->lpVtbl->Release(test_hal);
        if (hal.refs != 1 || device.refs != 1 || factory_refs != 1) return 1;
    }
    puts("{\"scope\":\"own COM contracts only\",\"iterations\":100,\"referencesBalanced\":true}");
    return 0;
}
