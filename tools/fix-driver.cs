using System;
using System.Runtime.InteropServices;

public class Program {
    [DllImport("newdev.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool UpdateDriverForPlugAndPlayDevices(
        IntPtr hwndParent,
        string HardwareId,
        string FullInfPath,
        uint InstallFlags,
        out bool bRebootRequired
    );

    public static void Main() {
        string hwId = "USB\\VID_16D1&PID_0400";
        string infPath = @"C:\Windows\System32\DriverStore\FileRepository\sfr.inf_amd64_189916598de7844b\sfr.inf";
        bool reboot = false;

        Console.WriteLine("[*] Atualizando driver para " + hwId + " com " + infPath);
        bool success = UpdateDriverForPlugAndPlayDevices(IntPtr.Zero, hwId, infPath, 1, out reboot);

        if (success) {
            Console.WriteLine("[+] Sucesso! Driver oficial da Suprema (SFRUSB) vinculado ao dispositivo.");
        } else {
            int err = Marshal.GetLastWin32Error();
            Console.WriteLine("[-] Falha ao atualizar driver. Erro Win32: " + err + " (0x" + err.ToString("X") + ")");
        }
    }
}
