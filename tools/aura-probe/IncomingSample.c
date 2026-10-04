/* Decode a marshalled incoming word, without assuming an RGB byte order. */
#include <stdio.h>
#include <string.h>
#include "IncomingSample.h"

HRESULT DecodeIncomingWord(const VARIANT *value, ULONG count, ULONG *word) {
    return DecodeIncomingWords(value, count, word, 1);
}

HRESULT DecodeIncomingWords(const VARIANT *value, ULONG count, ULONG *words, ULONG capacity) {
    if (!value || !words) return E_POINTER;
    if (!count || count > capacity || capacity > 64 ||
        V_VT(value) != (VT_ARRAY | VT_UI4) || !V_ARRAY(value)) return E_INVALIDARG;
    SAFEARRAY *array = V_ARRAY(value);
    VARTYPE element_type = VT_EMPTY;
    if (SafeArrayGetDim(array) != 1 || SafeArrayGetElemsize(array) != sizeof(ULONG) ||
        FAILED(SafeArrayGetVartype(array, &element_type)) || element_type != VT_UI4) return E_INVALIDARG;
    LONG lower = 0, upper = 0;
    if (FAILED(SafeArrayGetLBound(array, 1, &lower)) ||
        FAILED(SafeArrayGetUBound(array, 1, &upper)) ||
        (LONGLONG)upper - lower + 1 != count) return E_INVALIDARG;
    void *data = NULL;
    HRESULT hr = SafeArrayAccessData(array, &data);
    if (FAILED(hr)) return hr;
    memcpy(words, data, count * sizeof(ULONG));
    return SafeArrayUnaccessData(array);
}

/* Synthetic decoder fixtures only. Never invoke any HAL/SDK effect method. */
int CheckIncomingWordDecoder(void) {
    VARIANT value = {0};
    ULONG word = 0, expected = 0x12345678;
    int result = 1, cases = 0;
#define EXPECT(test) do { if (!(test)) { fprintf(stderr, "Incoming decoder check failed at %d\n", __LINE__); goto cleanup; } cases++; } while (0)
    EXPECT(DecodeIncomingWord(NULL, 1, &word) == E_POINTER);
    EXPECT(DecodeIncomingWord(&value, 1, NULL) == E_POINTER);
    EXPECT(DecodeIncomingWord(&value, 1, &word) == E_INVALIDARG);
    value.vt = VT_ARRAY | VT_UI4;
    EXPECT(DecodeIncomingWord(&value, 1, &word) == E_INVALIDARG);
    value.parray = SafeArrayCreateVector(VT_UI4, 7, 1);
    EXPECT(value.parray != NULL);
    LONG index = 7;
    EXPECT(SUCCEEDED(SafeArrayPutElement(value.parray, &index, &expected)));
    EXPECT(SUCCEEDED(DecodeIncomingWord(&value, 1, &word)) && word == expected);
    EXPECT(DecodeIncomingWord(&value, 0, &word) == E_INVALIDARG);
    EXPECT(DecodeIncomingWord(&value, 2, &word) == E_INVALIDARG);
    value.vt |= VT_BYREF;
    EXPECT(DecodeIncomingWord(&value, 1, &word) == E_INVALIDARG);
    value.vt = VT_ARRAY | VT_UI4;
    VariantClear(&value);
    value.vt = VT_ARRAY | VT_UI4; value.parray = SafeArrayCreateVector(VT_UI4, 0, 2);
    EXPECT(value.parray != NULL && DecodeIncomingWord(&value, 1, &word) == E_INVALIDARG);
    VariantClear(&value);
    /* Distinct simultaneous colors, nonzero lower bound and guarded output. */
    ULONG words[18];
    for (int i = 0; i < 18; i++) words[i] = 0xdeadbeef;
    value.vt = VT_ARRAY | VT_UI4; value.parray = SafeArrayCreateVector(VT_UI4, -4, 16);
    EXPECT(value.parray != NULL);
    for (LONG i = -4; i < 12; i++) {
        ULONG color = (i & 1) ? 0xff0000ff : 0xffff0000;
        EXPECT(SUCCEEDED(SafeArrayPutElement(value.parray, &i, &color)));
    }
    EXPECT(SUCCEEDED(DecodeIncomingWords(&value, 16, words + 1, 16)));
    EXPECT(words[0] == 0xdeadbeef && words[17] == 0xdeadbeef);
    for (int i = 0; i < 16; i++) EXPECT(words[i + 1] == ((i & 1) ? 0xff0000ffUL : 0xffff0000UL));
    EXPECT(DecodeIncomingWords(&value, 16, words, 15) == E_INVALIDARG);
    EXPECT(DecodeIncomingWords(&value, 15, words, 16) == E_INVALIDARG);
    EXPECT(DecodeIncomingWords(&value, 0, words, 16) == E_INVALIDARG);
    EXPECT(DecodeIncomingWords(&value, 16, words, 65) == E_INVALIDARG);
    EXPECT(DecodeIncomingWords(&value, 16, NULL, 16) == E_POINTER);
    VariantClear(&value);
    value.vt = VT_ARRAY | VT_UI4; value.parray = SafeArrayCreateVector(VT_UI4, 0, 0);
    EXPECT(value.parray != NULL && DecodeIncomingWord(&value, 1, &word) == E_INVALIDARG);
    VariantClear(&value);
    SAFEARRAYBOUND bounds[2] = {{1,0},{1,0}};
    value.vt = VT_ARRAY | VT_UI4; value.parray = SafeArrayCreate(VT_UI4, 2, bounds);
    EXPECT(value.parray != NULL && DecodeIncomingWord(&value, 1, &word) == E_INVALIDARG);
    VariantClear(&value);
    value.vt = VT_ARRAY | VT_UI4; value.parray = SafeArrayCreateVector(VT_I4, 0, 1);
    EXPECT(value.parray != NULL && DecodeIncomingWord(&value, 1, &word) == E_INVALIDARG);
    VariantClear(&value);
    value.vt = VT_ARRAY | VT_UI4; value.parray = SafeArrayCreateVector(VT_UI1, 0, 1);
    EXPECT(value.parray != NULL && DecodeIncomingWord(&value, 1, &word) == E_INVALIDARG);
    result = 0;
    printf("{\"scope\":\"synthetic incoming decoder fixtures only\",\"checks\":%d,\"passed\":true}\n", cases);
cleanup:
    value.vt &= ~VT_BYREF;
    VariantClear(&value);
    return result;
#undef EXPECT
}
