#include <windows.h>

#ifndef ARGUMENT_PRESENT
#define ARGUMENT_PRESENT(ArgumentPointer) ((CHAR*)(ArgumentPointer) != (CHAR*)NULL)
#endif

#include <winbio_types.h>
#include <winbio_err.h>
#include <winbio_adapter.h>
#include <stdio.h>

// GUID do BioMini WBF Sensor Adapter: {B3F484B6-6B22-4D3B-983C-111122223333}
static const GUID BIO_MINI_SENSOR_ADAPTER_GUID = 
{ 0xb3f484b6, 0x6b22, 0x4d3b, { 0x98, 0x3c, 0x11, 0x11, 0x22, 0x22, 0x33, 0x33 } };

#define PIPE_NAME L"\\\\.\\pipe\\BioMiniWbfPipe"

typedef struct _ADAPTER_CONTEXT {
    HANDLE hPipe;
    BOOL bCapturing;
    WINBIO_BIR* pLastSample;
} ADAPTER_CONTEXT, *PADAPTER_CONTEXT;

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

extern "C" __declspec(dllexport) HRESULT WINAPI WbioQuerySensorAdapterInterface(
    PWINBIO_SENSOR_INTERFACE *SensorInterface
) {
    if (SensorInterface == NULL) {
        return E_POINTER;
    }
    *SensorInterface = &g_SensorInterface;
    return S_OK;
}

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
    if (context->pLastSample) {
        free(context->pLastSample);
    }
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
