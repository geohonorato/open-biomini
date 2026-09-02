using System;
using System.Threading;
using System.Windows.Forms;
using Suprema;

public class Program {
    public static void Main() {
        Console.WriteLine("[*] Testando Suprema com Message Pump (Form)...");
        Form form = new Form();
        form.Show();
        form.Hide();

        var mgr = new UFScannerManager(form);
        UFS_STATUS st = mgr.Init();
        Console.WriteLine("[*] mgr.Init() = " + st);

        for (int i = 0; i < 20; i++) {
            Application.DoEvents();
            Thread.Sleep(100);
            mgr.Update();
            if (mgr.Scanners.Count > 0) break;
        }

        Console.WriteLine("[*] Scanners encontrados apos pump: " + mgr.Scanners.Count);

        if (mgr.Scanners.Count > 0) {
            var s = mgr.Scanners[0];
            Console.WriteLine("[+] Scanner ID: " + s.ID);
            Console.WriteLine("[*] Capturando...");
            UFS_STATUS cap = s.CaptureSingleImage();
            Console.WriteLine("[*] Resultado da captura: " + cap);
        } else {
            Console.WriteLine("[-] Nenhum scanner.");
        }

        mgr.Uninit();
        form.Dispose();
    }
}
