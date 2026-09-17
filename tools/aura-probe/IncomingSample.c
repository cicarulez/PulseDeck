/* Decode a marshalled incoming word, without assuming an RGB byte order. */
#include <stdio.h>
#include "IncomingSample.h"

HRESULT DecodeIncomingWord(const VARIANT *value, ULONG count, ULONG *word) {
    if (!value || !word) return E_POINTER;
    if (count != 1 || V_VT(value) != (VT_ARRAY | VT_UI4) || !V_ARRAY(value)) return E_INVALIDARG;
    SAFEARRAY *array = V_ARRAY(value);
    VARTYPE element_type = VT_EMPTY;
    if (SafeArrayGetDim(array) != 1 || SafeArrayGetElemsize(array) != sizeof(ULONG) ||
        FAILED(SafeArrayGetVartype(array, &element_type)) || element_type != VT_UI4) return E_INVALIDARG;
    LONG lower = 0, upper = 0;
    if (FAILED(SafeArrayGetLBound(array, 1, &lower)) ||
        FAILED(SafeArrayGetUBound(array, 1, &upper)) || lower != upper) return E_INVALIDARG;
    return SafeArrayGetElement(array, &lower, word);
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
