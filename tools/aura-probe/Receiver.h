#ifndef PULSEDECK_AURA_RECEIVER_H
#define PULSEDECK_AURA_RECEIVER_H
#include <windows.h>

typedef struct Receiver Receiver;
typedef struct ReceiverResult {
    DWORD process_id;
    LONG synthetic_samples;
    LONG unverified_callbacks;
    LONG has_sample;
    DWORD last_raw_color;
} ReceiverResult;

HRESULT StartReceiver(Receiver **receiver);
HRESULT StopReceiver(Receiver *receiver, ReceiverResult *result);
HRESULT PublishReceiverColor(Receiver *receiver, DWORD raw_color, BOOL synthetic);
HRESULT TestReceiverTransport(Receiver *receiver);
int RunReceiver(int argc, char **argv);
#endif
