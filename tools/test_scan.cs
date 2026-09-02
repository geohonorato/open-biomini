using System;
using OpenBioMini.Core;

public class Program {
    public static void Main() {
        Console.WriteLine("[*] Testando BioMiniController...");
        try {
            var bio = new BioMiniController();
            bool ok = bio.Initialize();
            Console.WriteLine("[*] bio.Initialize() = " + ok);
            Console.WriteLine("[*] bio.IsConnected = " + bio.IsConnected);
            Console.WriteLine("[*] bio.Model = " + bio.Model);
            Console.WriteLine("[*] bio.SerialNumber = " + bio.SerialNumber);

            if (bio.IsConnected) {
                Console.WriteLine("[+] Sucesso! Hardware conectado e pronto.");
            } else {
                Console.WriteLine("[-] Scanner nao conectado ao controller.");
            }
            bio.Dispose();
        } catch (Exception ex) {
            Console.WriteLine("[!] Excecao: " + ex.ToString());
        }
    }
}
