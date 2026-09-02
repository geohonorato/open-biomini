using System;
using System.Runtime.InteropServices;

public class Program {
    [DllImport("UFScanner.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int UFS_Init();

    [DllImport("UFScanner.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int UFS_Update();

    [DllImport("UFScanner.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int UFS_GetScannerNumber(out int nNumber);

    [DllImport("UFScanner.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int UFS_GetScannerHandle(int nIndex, out IntPtr hScanner);

    [DllImport("UFScanner.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int UFS_CaptureSingleImage(IntPtr hScanner);

    [DllImport("UFScanner.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int UFS_Uninit();

    public static void Main() {
        Console.WriteLine("[*] Testando UFS C API diretamente...");
        int st = UFS_Init();
        Console.WriteLine("[*] UFS_Init() = " + st);

        int upSt = UFS_Update();
        Console.WriteLine("[*] UFS_Update() = " + upSt);

        int count = 0;
        int numSt = UFS_GetScannerNumber(out count);
        Console.WriteLine("[*] UFS_GetScannerNumber() = " + numSt + ", Count = " + count);

        if (count > 0) {
            IntPtr hScanner;
            int hSt = UFS_GetScannerHandle(0, out hScanner);
            Console.WriteLine("[+] UFS_GetScannerHandle(0) = " + hSt + ", Handle = " + hScanner);

            if (hScanner != IntPtr.Zero) {
                Console.WriteLine("[*] Disparando UFS_CaptureSingleImage (acendendo LED do leitor)...");
                int capSt = UFS_CaptureSingleImage(hScanner);
                Console.WriteLine("[*] UFS_CaptureSingleImage = " + capSt);
            }
        } else {
            Console.WriteLine("[-] Nenhum leitor retornado por UFS_GetScannerNumber.");
        }

        UFS_Uninit();
    }
}
