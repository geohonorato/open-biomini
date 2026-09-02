#include <windows.h>

#ifndef ARGUMENT_PRESENT
#define ARGUMENT_PRESENT(ArgumentPointer) ((CHAR*)(ArgumentPointer) != (CHAR*)NULL)
#endif

#include <winbio_types.h>
#include <winbio_err.h>
#include <winbio_adapter.h>
#include <stdio.h>

static const GUID BIO_MINI_SENSOR_ADAPTER_GUID = 
{ 0xb3f484b6, 0x6b22, 0x4d3b, { 0x98, 0x3c, 0x11, 0x11, 0x22, 0x22, 0x33, 0x33 } };

static const GUID BIO_MINI_ENGINE_ADAPTER_GUID = 
{ 0xc4e595c7, 0x7c33, 0x4e4c, { 0xa9, 0x4d, 0x22, 0x22, 0x33, 0x33, 0x44, 0x44 } };

#define PIPE_NAME L"\\\\.\\pipe\\BioMiniWbfPipe"

typedef struct _ADAPTER_CONTEXT {
    HANDLE hPipe;
    BOOL bCapturing;
    WINBIO_BIR* pLastSample;
} ADAPTER_CONTEXT, *PADAPTER_CONTEXT;

// --- SENSOR ADAPTER METHODS ---
static HRESULT WINAPI SensorAdapterAttach(PWINBIO_PIPELINE Pipeline);
static HRESULT WINAPI SensorAdapterDetach(PWINBIO_PIPELINE Pipeline);
static HRESULT WINAPI SensorAdapterClearContext(PWINBIO_PIPELINE Pipeline);
static HRESULT WINAPI SensorAdapterQueryStatus(PWINBIO_PIPELINE Pipeline, PWINBIO_SENSOR_STATUS Status);
static HRESULT WINAPI SensorAdapterReset(PWINBIO_PIPELINE Pipeline);
static HRESULT WINAPI SensorAdapterSetMode(PWINBIO_PIPELINE Pipeline, WINBIO_SENSOR_MODE Mode);
static HRESULT WINAPI SensorAdapterSetIndicatorStatus(PWINBIO_PIPELINE Pipeline, WINBIO_INDICATOR_STATUS IndicatorStatus);
static HRESULT WINAPI SensorAdapterGetIndicatorStatus(PWINBIO_PIPELINE Pipeline, PWINBIO_INDICATOR_STATUS IndicatorStatus);
static HRESULT WINAPI SensorAdapterStartCapture(PWINBIO_PIPELINE Pipeline, WINBIO_BIR_PURPOSE Purpose, LPOVERLAPPED *Overlapped);
static HRESULT WINAPI SensorAdapterFinishCapture(PWINBIO_PIPELINE Pipeline, PWINBIO_REJECT_DETAIL RejectDetail);
static HRESULT WINAPI SensorAdapterExportSensorData(PWINBIO_PIPELINE Pipeline, PWINBIO_BIR *SampleBuffer, PSIZE_T SampleSize);
static HRESULT WINAPI SensorAdapterCancel(PWINBIO_PIPELINE Pipeline);
static HRESULT WINAPI SensorAdapterPushDataToEngine(PWINBIO_PIPELINE Pipeline, WINBIO_BIR_PURPOSE Purpose, WINBIO_BIR_DATA_FLAGS Flags, PWINBIO_REJECT_DETAIL RejectDetail);
static HRESULT WINAPI SensorAdapterControlUnit(PWINBIO_PIPELINE Pipeline, ULONG ControlCode, PUCHAR SendBuffer, SIZE_T SendBufferSize, PUCHAR ReceiveBuffer, SIZE_T ReceiveBufferSize, PSIZE_T ReceiveDataSize, PULONG OperationStatus);
static HRESULT WINAPI SensorAdapterControlUnitPrivileged(PWINBIO_PIPELINE Pipeline, ULONG ControlCode, PUCHAR SendBuffer, SIZE_T SendBufferSize, PUCHAR ReceiveBuffer, SIZE_T ReceiveBufferSize, PSIZE_T ReceiveDataSize, PULONG OperationStatus);

static WINBIO_SENSOR_INTERFACE g_SensorInterface = {
    WINBIO_SENSOR_INTERFACE_VERSION_1,
    WINBIO_ADAPTER_TYPE_SENSOR,
    sizeof(WINBIO_SENSOR_INTERFACE),
    BIO_MINI_SENSOR_ADAPTER_GUID,
    SensorAdapterAttach,
    SensorAdapterDetach,
    SensorAdapterClearContext,
    SensorAdapterQueryStatus,
    SensorAdapterReset,
    SensorAdapterSetMode,
    SensorAdapterSetIndicatorStatus,
    SensorAdapterGetIndicatorStatus,
    SensorAdapterStartCapture,
    SensorAdapterFinishCapture,
    SensorAdapterExportSensorData,
    SensorAdapterCancel,
    SensorAdapterPushDataToEngine,
    SensorAdapterControlUnit,
    SensorAdapterControlUnitPrivileged
};

// --- ENGINE ADAPTER METHODS ---
static HRESULT WINAPI EngineAdapterAttach(PWINBIO_PIPELINE Pipeline) { return S_OK; }
static HRESULT WINAPI EngineAdapterDetach(PWINBIO_PIPELINE Pipeline) { return S_OK; }
static HRESULT WINAPI EngineAdapterClearContext(PWINBIO_PIPELINE Pipeline) { return S_OK; }
static HRESULT WINAPI EngineAdapterQueryPreferredFormat(PWINBIO_PIPELINE Pipeline, PWINBIO_REGISTERED_FORMAT StandardFormat, PWINBIO_UUID VendorFormat) {
    if (StandardFormat) {
        StandardFormat->Owner = WINBIO_ANSI_381_FORMAT_OWNER;
        StandardFormat->Type = WINBIO_ANSI_381_FORMAT_TYPE;
    }
    return S_OK;
}
static HRESULT WINAPI EngineAdapterQueryIndexVectorSize(PWINBIO_PIPELINE Pipeline, PSIZE_T IndexElementCount) { if (IndexElementCount) *IndexElementCount = 0; return S_OK; }
static HRESULT WINAPI EngineAdapterQueryHashAlgorithms(PWINBIO_PIPELINE Pipeline, PSIZE_T AlgorithmCount, PSIZE_T AlgorithmBufferSize, PUCHAR *AlgorithmBuffer) { if (AlgorithmCount) *AlgorithmCount = 0; if (AlgorithmBufferSize) *AlgorithmBufferSize = 0; return S_OK; }
static HRESULT WINAPI EngineAdapterSetHashAlgorithm(PWINBIO_PIPELINE Pipeline, SIZE_T AlgorithmBufferSize, PUCHAR AlgorithmBuffer) { return S_OK; }
static HRESULT WINAPI EngineAdapterQuerySampleHint(PWINBIO_PIPELINE Pipeline, PSIZE_T SampleHint) { if (SampleHint) *SampleHint = 1; return S_OK; }
static HRESULT WINAPI EngineAdapterAcceptSampleData(PWINBIO_PIPELINE Pipeline, PWINBIO_BIR SampleBuffer, SIZE_T SampleSize, WINBIO_BIR_PURPOSE Purpose, PWINBIO_REJECT_DETAIL RejectDetail) { if (RejectDetail) *RejectDetail = 0; return S_OK; }
static HRESULT WINAPI EngineAdapterExportEngineData(PWINBIO_PIPELINE Pipeline, WINBIO_BIR_DATA_FLAGS Flags, PWINBIO_BIR *SampleBuffer, PSIZE_T SampleSize) { return E_NOTIMPL; }
static HRESULT WINAPI EngineAdapterVerifyFeatureSet(PWINBIO_PIPELINE Pipeline, PWINBIO_IDENTITY Identity, WINBIO_BIOMETRIC_SUBTYPE SubFactor, PBOOLEAN Match, PUCHAR *PayloadBlob, PSIZE_T PayloadBlobSize, PUCHAR *HashValue, PSIZE_T HashSize, PWINBIO_REJECT_DETAIL RejectDetail) { if (Match) *Match = TRUE; if (RejectDetail) *RejectDetail = 0; return S_OK; }
static HRESULT WINAPI EngineAdapterIdentifyFeatureSet(PWINBIO_PIPELINE Pipeline, PWINBIO_IDENTITY Identity, PWINBIO_BIOMETRIC_SUBTYPE SubFactor, PUCHAR *PayloadBlob, PSIZE_T PayloadBlobSize, PUCHAR *HashValue, PSIZE_T HashSize, PWINBIO_REJECT_DETAIL RejectDetail) { if (RejectDetail) *RejectDetail = 0; return S_OK; }
static HRESULT WINAPI EngineAdapterCreateEnrollment(PWINBIO_PIPELINE Pipeline) { return S_OK; }
static HRESULT WINAPI EngineAdapterUpdateEnrollment(PWINBIO_PIPELINE Pipeline, PWINBIO_REJECT_DETAIL RejectDetail) { if (RejectDetail) *RejectDetail = 0; return S_OK; }
static HRESULT WINAPI EngineAdapterGetEnrollmentStatus(PWINBIO_PIPELINE Pipeline, PWINBIO_REJECT_DETAIL RejectDetail) { if (RejectDetail) *RejectDetail = 0; return S_OK; }
static HRESULT WINAPI EngineAdapterGetEnrollmentHash(PWINBIO_PIPELINE Pipeline, PUCHAR *HashValue, PSIZE_T HashSize) { if (HashSize) *HashSize = 0; return S_OK; }
static HRESULT WINAPI EngineAdapterCheckForDuplicate(PWINBIO_PIPELINE Pipeline, PWINBIO_IDENTITY Identity, PWINBIO_BIOMETRIC_SUBTYPE SubFactor, PBOOLEAN Duplicate) { if (Duplicate) *Duplicate = FALSE; return S_OK; }
static HRESULT WINAPI EngineAdapterCommitEnrollment(PWINBIO_PIPELINE Pipeline, PWINBIO_IDENTITY Identity, WINBIO_BIOMETRIC_SUBTYPE SubFactor, PUCHAR PayloadBlob, SIZE_T PayloadBlobSize) { return S_OK; }
static HRESULT WINAPI EngineAdapterDiscardEnrollment(PWINBIO_PIPELINE Pipeline) { return S_OK; }
static HRESULT WINAPI EngineAdapterControlUnit(PWINBIO_PIPELINE Pipeline, ULONG ControlCode, PUCHAR SendBuffer, SIZE_T SendBufferSize, PUCHAR ReceiveBuffer, SIZE_T ReceiveBufferSize, PSIZE_T ReceiveDataSize, PULONG OperationStatus) { return S_OK; }
static HRESULT WINAPI EngineAdapterControlUnitPrivileged(PWINBIO_PIPELINE Pipeline, ULONG ControlCode, PUCHAR SendBuffer, SIZE_T SendBufferSize, PUCHAR ReceiveBuffer, SIZE_T ReceiveBufferSize, PSIZE_T ReceiveDataSize, PULONG OperationStatus) { return S_OK; }

static WINBIO_ENGINE_INTERFACE g_EngineInterface = {
    WINBIO_ENGINE_INTERFACE_VERSION_1,
    WINBIO_ADAPTER_TYPE_ENGINE,
    sizeof(WINBIO_ENGINE_INTERFACE),
    BIO_MINI_ENGINE_ADAPTER_GUID,
    EngineAdapterAttach,
    EngineAdapterDetach,
    EngineAdapterClearContext,
    EngineAdapterQueryPreferredFormat,
    EngineAdapterQueryIndexVectorSize,
    EngineAdapterQueryHashAlgorithms,
    EngineAdapterSetHashAlgorithm,
    EngineAdapterQuerySampleHint,
    EngineAdapterAcceptSampleData,
    EngineAdapterExportEngineData,
    EngineAdapterVerifyFeatureSet,
    EngineAdapterIdentifyFeatureSet,
    EngineAdapterCreateEnrollment,
    EngineAdapterUpdateEnrollment,
    EngineAdapterGetEnrollmentStatus,
    EngineAdapterGetEnrollmentHash,
    EngineAdapterCheckForDuplicate,
    EngineAdapterCommitEnrollment,
    EngineAdapterDiscardEnrollment,
    EngineAdapterControlUnit,
    EngineAdapterControlUnitPrivileged
};

// --- EXPORTS ---

HRESULT WINAPI WbioQuerySensorInterface(
    PWINBIO_SENSOR_INTERFACE *SensorInterface
) {
    if (SensorInterface == NULL) return E_POINTER;
    *SensorInterface = &g_SensorInterface;
    return S_OK;
}

HRESULT WINAPI WbioQuerySensorAdapterInterface(
    PWINBIO_SENSOR_INTERFACE *SensorInterface
) {
    return WbioQuerySensorInterface(SensorInterface);
}

HRESULT WINAPI WbioQueryEngineInterface(
    PWINBIO_ENGINE_INTERFACE *EngineInterface
) {
    if (EngineInterface == NULL) return E_POINTER;
    *EngineInterface = &g_EngineInterface;
    return S_OK;
}

HRESULT WINAPI WbioQueryEngineAdapterInterface(
    PWINBIO_ENGINE_INTERFACE *EngineInterface
) {
    return WbioQueryEngineInterface(EngineInterface);
}

// --- SENSOR IMPLEMENTATION ---

static HRESULT WINAPI SensorAdapterAttach(PWINBIO_PIPELINE Pipeline) {
    if (Pipeline == NULL) return E_POINTER;
    PADAPTER_CONTEXT context = (PADAPTER_CONTEXT)malloc(sizeof(ADAPTER_CONTEXT));
    if (!context) return E_OUTOFMEMORY;
    memset(context, 0, sizeof(ADAPTER_CONTEXT));
    Pipeline->SensorContext = (PWINIBIO_SENSOR_CONTEXT)context;
    return S_OK;
}

static HRESULT WINAPI SensorAdapterDetach(PWINBIO_PIPELINE Pipeline) {
    if (Pipeline == NULL || Pipeline->SensorContext == NULL) return E_POINTER;
    PADAPTER_CONTEXT context = (PADAPTER_CONTEXT)Pipeline->SensorContext;
    if (context->pLastSample) free(context->pLastSample);
    free(context);
    Pipeline->SensorContext = NULL;
    return S_OK;
}

static HRESULT WINAPI SensorAdapterClearContext(PWINBIO_PIPELINE Pipeline) {
    if (Pipeline == NULL || Pipeline->SensorContext == NULL) return E_POINTER;
    PADAPTER_CONTEXT context = (PADAPTER_CONTEXT)Pipeline->SensorContext;
    if (context->pLastSample) {
        free(context->pLastSample);
        context->pLastSample = NULL;
    }
    return S_OK;
}

static HRESULT WINAPI SensorAdapterQueryStatus(PWINBIO_PIPELINE Pipeline, PWINBIO_SENSOR_STATUS Status) {
    if (Pipeline == NULL || Status == NULL) return E_POINTER;
    *Status = WINBIO_SENSOR_READY;
    return S_OK;
}

static HRESULT WINAPI SensorAdapterReset(PWINBIO_PIPELINE Pipeline) {
    return S_OK;
}

static HRESULT WINAPI SensorAdapterSetMode(PWINBIO_PIPELINE Pipeline, WINBIO_SENSOR_MODE Mode) {
    return S_OK;
}

static HRESULT WINAPI SensorAdapterSetIndicatorStatus(PWINBIO_PIPELINE Pipeline, WINBIO_INDICATOR_STATUS IndicatorStatus) {
    return S_OK;
}

static HRESULT WINAPI SensorAdapterGetIndicatorStatus(PWINBIO_PIPELINE Pipeline, PWINBIO_INDICATOR_STATUS IndicatorStatus) {
    if (IndicatorStatus) *IndicatorStatus = WINBIO_INDICATOR_ON;
    return S_OK;
}

static HRESULT WINAPI SensorAdapterStartCapture(PWINBIO_PIPELINE Pipeline, WINBIO_BIR_PURPOSE Purpose, LPOVERLAPPED *Overlapped) {
    if (Pipeline == NULL || Pipeline->SensorContext == NULL) return E_POINTER;
    PADAPTER_CONTEXT context = (PADAPTER_CONTEXT)Pipeline->SensorContext;
    context->bCapturing = TRUE;
    if (Overlapped) *Overlapped = NULL;
    return S_OK;
}

static HRESULT WINAPI SensorAdapterFinishCapture(PWINBIO_PIPELINE Pipeline, PWINBIO_REJECT_DETAIL RejectDetail) {
    if (Pipeline == NULL || Pipeline->SensorContext == NULL) return E_POINTER;
    PADAPTER_CONTEXT context = (PADAPTER_CONTEXT)Pipeline->SensorContext;

    HANDLE hPipe = CreateFileW(PIPE_NAME, GENERIC_READ | GENERIC_WRITE, 0, NULL, OPEN_EXISTING, 0, NULL);
    if (hPipe == INVALID_HANDLE_VALUE) {
        if (RejectDetail) *RejectDetail = 0;
        return WINBIO_E_DEVICE_FAILURE;
    }

    char cmd[] = "SCAN";
    DWORD bytesWritten = 0;
    WriteFile(hPipe, cmd, (DWORD)strlen(cmd), &bytesWritten, NULL);

    unsigned char templateBuf[1024] = {0};
    DWORD bytesRead = 0;
    ReadFile(hPipe, templateBuf, sizeof(templateBuf), &bytesRead, NULL);
    CloseHandle(hPipe);

    if (bytesRead <= 0) {
        if (RejectDetail) *RejectDetail = 0;
        return WINBIO_E_BAD_CAPTURE;
    }

    SIZE_T totalSize = sizeof(WINBIO_BIR) + sizeof(WINBIO_BIR_HEADER) + bytesRead;
    if (context->pLastSample) free(context->pLastSample);
    context->pLastSample = (PWINBIO_BIR)malloc(totalSize);
    if (!context->pLastSample) return E_OUTOFMEMORY;
    memset(context->pLastSample, 0, totalSize);

    context->pLastSample->HeaderBlock.Size = sizeof(WINBIO_BIR_HEADER);
    context->pLastSample->HeaderBlock.Offset = sizeof(WINBIO_BIR);
    context->pLastSample->StandardDataBlock.Size = (ULONG)bytesRead;
    context->pLastSample->StandardDataBlock.Offset = (ULONG)(sizeof(WINBIO_BIR) + sizeof(WINBIO_BIR_HEADER));

    PWINBIO_BIR_HEADER header = (PWINBIO_BIR_HEADER)((PUCHAR)context->pLastSample + sizeof(WINBIO_BIR));
    header->ValidFields = WINBIO_BIR_FIELD_BIOMETRIC_TYPE | WINBIO_BIR_FIELD_QUALITY;
    header->HeaderVersion = WINBIO_CBEFF_HEADER_VERSION;
    header->PatronHeaderVersion = WINBIO_PATRON_HEADER_VERSION;
    header->Type = WINBIO_TYPE_FINGERPRINT;
    header->DataFlags = WINBIO_DATA_FLAG_RAW;
    header->Purpose = WINBIO_NO_PURPOSE_AVAILABLE;
    header->DataQuality = 90;

    PUCHAR dataBlock = (PUCHAR)context->pLastSample + sizeof(WINBIO_BIR) + sizeof(WINBIO_BIR_HEADER);
    memcpy(dataBlock, templateBuf, bytesRead);

    context->bCapturing = FALSE;
    if (RejectDetail) *RejectDetail = 0;
    return S_OK;
}

static HRESULT WINAPI SensorAdapterExportSensorData(PWINBIO_PIPELINE Pipeline, PWINBIO_BIR *SampleBuffer, PSIZE_T SampleSize) {
    if (Pipeline == NULL || Pipeline->SensorContext == NULL || SampleBuffer == NULL || SampleSize == NULL) return E_POINTER;
    PADAPTER_CONTEXT context = (PADAPTER_CONTEXT)Pipeline->SensorContext;
    if (!context->pLastSample) return WINBIO_E_NO_CAPTURE_DATA;

    SIZE_T totalSize = sizeof(WINBIO_BIR) + sizeof(WINBIO_BIR_HEADER) + context->pLastSample->StandardDataBlock.Size;
    PWINBIO_BIR copyBuf = (PWINBIO_BIR)malloc(totalSize);
    if (!copyBuf) return E_OUTOFMEMORY;
    memcpy(copyBuf, context->pLastSample, totalSize);

    *SampleBuffer = copyBuf;
    *SampleSize = totalSize;
    return S_OK;
}

static HRESULT WINAPI SensorAdapterCancel(PWINBIO_PIPELINE Pipeline) {
    if (Pipeline == NULL || Pipeline->SensorContext == NULL) return E_POINTER;
    PADAPTER_CONTEXT context = (PADAPTER_CONTEXT)Pipeline->SensorContext;
    context->bCapturing = FALSE;
    return S_OK;
}

static HRESULT WINAPI SensorAdapterPushDataToEngine(PWINBIO_PIPELINE Pipeline, WINBIO_BIR_PURPOSE Purpose, WINBIO_BIR_DATA_FLAGS Flags, PWINBIO_REJECT_DETAIL RejectDetail) {
    return S_OK;
}

static HRESULT WINAPI SensorAdapterControlUnit(PWINBIO_PIPELINE Pipeline, ULONG ControlCode, PUCHAR SendBuffer, SIZE_T SendBufferSize, PUCHAR ReceiveBuffer, SIZE_T ReceiveBufferSize, PSIZE_T ReceiveDataSize, PULONG OperationStatus) {
    return S_OK;
}

static HRESULT WINAPI SensorAdapterControlUnitPrivileged(PWINBIO_PIPELINE Pipeline, ULONG ControlCode, PUCHAR SendBuffer, SIZE_T SendBufferSize, PUCHAR ReceiveBuffer, SIZE_T ReceiveBufferSize, PSIZE_T ReceiveDataSize, PULONG OperationStatus) {
    return S_OK;
}
