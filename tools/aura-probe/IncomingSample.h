#ifndef PULSEDECK_INCOMING_SAMPLE_H
#define PULSEDECK_INCOMING_SAMPLE_H
#include <windows.h>
#include <oleauto.h>
HRESULT DecodeIncomingWord(const VARIANT *value, ULONG count, ULONG *word);
int CheckIncomingWordDecoder(void);
#endif
