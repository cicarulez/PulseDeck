/* Experimental, process-local COM HAL. No hardware or network access. */
#define COBJMACROS
#include <windows.h>
#include <oleauto.h>
#include <stdio.h>
#include "DiscoveryHal.h"

static const GUID probe_clsid = {0x702d21b6,0x3a25,0x4d2c,{0x9f,0x73,0xf6,0x4c,0x78,0xe2,0x12,0xa8}};
static const GUID hal_iid = {0xf2c8d5b4,0x3854,0x4325,{0x8a,0x4f,0xfd,0x7c,0x50,0x72,0xe3,0xb9}};
static LONG activations, enumerations, capabilities, effect_requests, sync_requests;
static LONG factory_refs = 1;
static BOOL empty_devices;
static DWORD cookie;
static Receiver *receiver;
void SetProbeReceiver(Receiver *target) { receiver = target; }
static const GUID device_iid = {0x61711778,0xab59,0x4026,{0x89,0xe8,0x7a,0x63,0x42,0x2c,0x29,0xc2}};
typedef struct Device Device;
typedef struct DeviceVtbl {
    HRESULT (STDMETHODCALLTYPE *QueryInterface)(Device*, REFIID, void**);
    ULONG (STDMETHODCALLTYPE *AddRef)(Device*);
    ULONG (STDMETHODCALLTYPE *Release)(Device*);
    HRESULT (STDMETHODCALLTYPE *GetCapability)(Device*, BSTR*);
    HRESULT (STDMETHODCALLTYPE *SetEffect)(Device*, ULONG, ULONG*, ULONG);
    HRESULT (STDMETHODCALLTYPE *Synchronize)(Device*, ULONG, ULONGLONG);
} DeviceVtbl;
struct Device { const DeviceVtbl *lpVtbl; LONG refs; };
static ULONG STDMETHODCALLTYPE device_add(Device *self) { return InterlockedIncrement(&self->refs); }
static ULONG STDMETHODCALLTYPE device_release(Device *self) { return InterlockedDecrement(&self->refs); }
static HRESULT STDMETHODCALLTYPE device_query(Device *self, REFIID iid, void **result) {
    if (!result) return E_POINTER;
    *result = NULL;
    if (!IsEqualGUID(iid, &IID_IUnknown) && !IsEqualGUID(iid, &device_iid)) return E_NOINTERFACE;
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
static const DeviceVtbl device_vtbl={device_query,device_add,device_release,device_capability,device_effect,device_sync};
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
