#ifndef PULSEDECK_DISCOVERY_HAL_H
#define PULSEDECK_DISCOVERY_HAL_H
#include <windows.h>
#include "Receiver.h"

#define PROBE_GUID_TEXT L"{702D21B6-3A25-4D2C-9F73-F64C78E212A8}"
#define PROBE_DEVICE_NAME L"PulseDeck Virtual Probe"
/* Installed LightingService maps 0x64000 to EXTERNAL_GENERAL; 0 means All. */
#define PROBE_DEVICE_TYPE 0x64000UL

typedef struct ProbeStats {
    LONG activations;
    LONG enumerations;
    LONG capabilities;
    LONG effect_requests;
    LONG sync_requests;
    LONG hal_refs;
    LONG device_refs;
    LONG factory_refs;
    LONG last_effect_method;
    ULONG last_effect_id;
    ULONG last_effect_count;
    ULONG last_effect_variant;
    LONG raw_samples;
    ULONG raw_word;
    ULONGLONG raw_sample_tick;
} ProbeStats;

int CheckProbeContracts(void);
int RunComServer(const char *parent_pid, const char *event_suffix, const char *lifetime);
int CheckRemoteContracts(void);
int CheckRemoteAbsent(void);
HRESULT RegisterLocalProbe(void);
BOOL ProbeHasClients(void);
void SetProbeReceiver(Receiver *receiver);
HRESULT RegisterProbe(BOOL empty);
HRESULT UnregisterProbe(void);
ProbeStats GetProbeStats(void);
#endif
