using System;
using System.IO;
using OpenBioMini;

namespace OpenBioMini.Cli {
    class Program {
        static int Main(string[] args) {
            Console.WriteLine("==================================================");
            Console.WriteLine("🔬 OPEN-BIOMINI CLI TOOL v1.0.0");
            Console.WriteLine("   Criado por: Geovanni Honorato (@geohonorato)");
            Console.WriteLine("   GitHub: https://github.com/geohonorato/open-biomini");
            Console.WriteLine("==================================================");

            if (args.Length == 0 || args[0] == "-h" || args[0] == "--help") {
                PrintHelp();
                return 0;
            }

            string command = args[0].ToLower();

            using (BioMiniController controller = new BioMiniController()) {
                if (command == "status") {
                    Console.Write("[*] Verificando leitor USB... ");
                    if (controller.Initialize()) {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("CONECTADO!");
                        Console.ResetColor();
                        Console.WriteLine("    Modelo : " + controller.ScannerModel);
                        Console.WriteLine("    Serial : " + controller.ScannerSerial);
                        return 0;
                    } else {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("FALHA AO CONECTAR");
                        Console.ResetColor();
                        Console.WriteLine("    Verifique o cabo USB e o driver instalado.");
                        return 1;
                    }
                }
                else if (command == "scan" || command == "capture") {
                    string outputFile = "digital.png";
                    for (int i = 1; i < args.Length; i++) {
                        if ((args[i] == "-o" || args[i] == "--output") && i + 1 < args.Length) {
                            outputFile = args[i + 1];
                        }
                    }

                    Console.Write("[*] Inicializando leitor... ");
                    if (!controller.Initialize()) {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("FALHA NA CONEXAO");
                        Console.ResetColor();
                        return 1;
                    }
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("OK");
                    Console.ResetColor();

                    Console.WriteLine("[*] Posicione o dedo no sensor (LED aceso)...");
                    ScanResult res = controller.Capture(6000);

                    if (res.Success) {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine(">>> ✓ CAPTURA CONCLUIDA COM SUCESSO!");
                        Console.ResetColor();
                        Console.WriteLine("    Qualidade     : " + res.Quality + "%");
                        Console.WriteLine("    Tamanho Temp. : " + res.TemplateSize + " bytes");

                        if (res.ImageBitmap != null) {
                            res.ImageBitmap.Save(outputFile);
                            Console.WriteLine("    Imagem Salva  : " + Path.GetFullPath(outputFile));
                        }

                        string b64File = Path.ChangeExtension(outputFile, ".b64");
                        string b64 = Convert.ToBase64String(res.Template);
                        File.WriteAllText(b64File, b64);
                        Console.WriteLine("    Template B64  : " + Path.GetFullPath(b64File));
                        return 0;
                    } else {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(">>> ✗ ERRO: " + res.ErrorMessage);
                        Console.ResetColor();
                        return 1;
                    }
                }
                else if (command == "match" || command == "verify") {
                    if (args.Length < 3) {
                        Console.WriteLine("Uso: biomini match <arquivo_template1.b64> <arquivo_template2.b64>");
                        return 1;
                    }

                    if (!File.Exists(args[1]) || !File.Exists(args[2])) {
                        Console.WriteLine("Erro: Um ou ambos os arquivos de template não foram encontrados.");
                        return 1;
                    }

                    if (!controller.Initialize()) {
                        Console.WriteLine("Erro ao inicializar motor de matching.");
                        return 1;
                    }

                    byte[] tA = Convert.FromBase64String(File.ReadAllText(args[1]).Trim());
                    byte[] tB = Convert.FromBase64String(File.ReadAllText(args[2]).Trim());

                    bool match = controller.Verify(tA, tA.Length, tB, tB.Length);
                    if (match) {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine(">>> ✅ MATCH CONFIRMADO: As duas digitais PERTENCEM à mesma pessoa!");
                        Console.ResetColor();
                        return 0;
                    } else {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(">>> ❌ MATCH NEGATIVO: Digitais DIFERENTES.");
                        Console.ResetColor();
                        return 2;
                    }
                }
                else {
                    Console.WriteLine("Comando desconhecido: " + command);
                    PrintHelp();
                    return 1;
                }
            }
        }

        static void PrintHelp() {
            Console.WriteLine("Comandos disponíveis:");
            Console.WriteLine("  biomini status                 Verifica conexão com o leitor físico");
            Console.WriteLine("  biomini scan [-o <arquivo>]    Dispara captura óptica e salva imagem + template");
            Console.WriteLine("  biomini match <t1.b64> <t2.b64> Compara dois templates extraídos");
        }
    }
}
