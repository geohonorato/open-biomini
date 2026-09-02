using System;
using System.Runtime.InteropServices;
using System.Text;

class EnumerateDevices {
    [DllImport("setupapi.dll", SetLastError = true)]
    static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, uint Flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiEnumDeviceInterfaces(IntPtr DeviceInfoSet, IntPtr DeviceInfoData, ref Guid InterfaceClassGuid, uint MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr DeviceInfoSet, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, IntPtr DeviceInterfaceDetailData, uint DeviceInterfaceDetailDataSize, ref uint RequiredSize, IntPtr DeviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

    [StructLayout(LayoutKind.Sequential)]
    struct SP_DEVICE_INTERFACE_DATA {
        public uint cbSize;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }

    static void Main() {
        // GUID_DEVINTERFACE_USB_DEVICE {A5DCBF10-6530-11D2-901F-00C04FB951ED}
        Guid usbGuid = new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED");
        IntPtr hDevInfo = SetupDiGetClassDevs(ref usbGuid, IntPtr.Zero, IntPtr.Zero, 0x12); // DIGCF_PRESENT | DIGCF_DEVICEINTERFACE
        if (hDevInfo.ToInt64() == -1) {
            Console.WriteLine("SetupDiGetClassDevs failed");
            return;
        }

        SP_DEVICE_INTERFACE_DATA ifData = new SP_DEVICE_INTERFACE_DATA();
        ifData.cbSize = (uint)Marshal.SizeOf(ifData);
        uint index = 0;

        while (SetupDiEnumDeviceInterfaces(hDevInfo, IntPtr.Zero, ref usbGuid, index, ref ifData)) {
            uint reqSize = 0;
            SetupDiGetDeviceInterfaceDetail(hDevInfo, ref ifData, IntPtr.Zero, 0, ref reqSize, IntPtr.Zero);
            if (reqSize > 0) {
                IntPtr detailBuffer = Marshal.AllocHGlobal((int)reqSize);
                Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 4 + Marshal.SystemDefaultCharSize); // cbSize
                if (SetupDiGetDeviceInterfaceDetail(hDevInfo, ref ifData, detailBuffer, reqSize, ref reqSize, IntPtr.Zero)) {
                    IntPtr pPath = new IntPtr(detailBuffer.ToInt64() + 4);
                    string devicePath = Marshal.PtrToStringAuto(pPath);
                    if (devicePath.ToLower().Contains("16d1")) {
                        Console.WriteLine("ACHOU DISPOSITIVO SUPREMA: " + devicePath);
                    }
                }
                Marshal.FreeHGlobal(detailBuffer);
            }
            index++;
        }
        SetupDiDestroyDeviceInfoList(hDevInfo);
        Console.WriteLine("Fim da enumeracao. Total USB interfaces verificadas: " + index);
    }
}
