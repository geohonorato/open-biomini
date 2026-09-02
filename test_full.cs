using System;
using System.IO;
using Suprema;

public class Program {
    public static void Main() {
        Console.WriteLine("[*] Testando Suprema UFScannerManager...");
        try {
            var mgr = new UFScannerManager(null);
            UFS_STATUS st = mgr.Init();
            Console.WriteLine("[*] mgr.Init() = " + st + " (" + (int)st + ")");

            UFS_STATUS up = mgr.Update();
            Console.WriteLine("[*] mgr.Update() = " + up + " (" + (int)up + ")");

            int count = mgr.Scanners.Count;
            Console.WriteLine("[*] mgr.Scanners.Count = " + count);

            if (count > 0) {
                var s = mgr.Scanners[0];
                Console.WriteLine("[+] Scanner 0 ID = " + s.ID);
                Console.WriteLine("[*] Disparando captura...");
                UFS_STATUS cap = s.CaptureSingleImage();
                Console.WriteLine("[*] s.CaptureSingleImage() = " + cap + " (" + (int)cap + ")");
            } else {
                Console.WriteLine("[-] Nenhum scanner listado em mgr.Scanners.");
            }
            mgr.Uninit();
        } catch (Exception ex) {
            Console.WriteLine("[!] Exception: " + ex.Message);
        }
    }
}
