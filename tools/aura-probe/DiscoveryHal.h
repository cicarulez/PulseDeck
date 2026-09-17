#ifndef PULSEDECK_DISCOVERY_HAL_H
#define PULSEDECK_DISCOVERY_HAL_H
#include <windows.h>
#include "Receiver.h"

#define PROBE_GUID_TEXT L"{702D21B6-3A25-4D2C-9F73-F64C78E212A8}"
#define PROBE_DEVICE_NAME L"PulseDeck Virtual Probe"

typedef struct ProbeStats {
    LONG activations;
    LONG enumerations;
    LONG capabilities;
    LONG effect_requests;
    LONG sync_requests;
    LONG hal_refs;
    LONG device_refs;
    LONG factory_refs;
} ProbeStats;

int CheckProbeContracts(void);
int RunComServer(const char *parent_pid, const char *event_suffix);
int CheckRemoteContracts(void);
int CheckRemoteAbsent(void);
void SetProbeReceiver(Receiver *receiver);
HRESULT RegisterProbe(BOOL empty);
HRESULT UnregisterProbe(void);
ProbeStats GetProbeStats(void);
#endif
