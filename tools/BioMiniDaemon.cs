using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

public class BioMiniDaemon {
    [DllImport("UFScanner.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int UFS_Init();

    [DllImport("UFScanner.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int UFS_Update();

    [DllImport("UFScanner.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int UFS_GetScannerNumber(out int nNumber);

    [DllImport("UFScanner.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int UFS_GetScannerHandle(int nIndex, out IntPtr hScanner);

    [DllImport("UFScanner.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int UFS_GetScannerType(IntPtr hScanner, out int nType);

    [DllImport("UFScanner.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int UFS_GetScannerID(IntPtr hScanner, byte[] szID);

    [DllImport("UFScanner.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int UFS_SetParameter(IntPtr hScanner, int nParam, ref int nValue);

    [DllImport("UFScanner.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int UFS_CaptureSingleImage(IntPtr hScanner);

    [DllImport("UFScanner.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int UFS_Extract(IntPtr hScanner, byte[] pTemplate, out int nTemplateSize, out int nQuality);

    [DllImport("UFScanner.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int UFS_AbortCapturing(IntPtr hScanner);

    [DllImport("UFScanner.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int UFS_Uninit();

    private const int UFS_PARAM_TIMEOUT = 201;
    private const int UFS_PARAM_BRIGHTNESS = 202;
    private const int UFS_PARAM_SENSITIVITY = 203;

    private static bool s_Running = true;

    public static void Main(string[] args) {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("==================================================");
        Console.WriteLine("🔬 SUPREMA BIOMINI NATIVE DAEMON v2.1");
        Console.WriteLine("   Auto-Recovery • Touch Detection • ESC/POS Hook");
        Console.WriteLine("==================================================");

        AppDomain.CurrentDomain.ProcessExit += (s, e) => {
            s_Running = false;
            try { UFS_Uninit(); } catch {}
        };

        while (s_Running) {
            IntPtr hScanner = IntPtr.Zero;
            try {
                Console.WriteLine("\n[*] Inicializando UFS SDK Nativo...");
                int initSt = UFS_Init();
                Console.WriteLine("[*] UFS_Init() = " + initSt);

                int count = 0;
                int retry = 0;

                while (s_Running && count == 0) {
                    UFS_Update();
                    UFS_GetScannerNumber(out count);

                    if (count > 0) break;

                    if (retry % 5 == 0) {
                        Console.WriteLine("[⏳ AGUARDANDO DISPOSITIVO] Conecte o cabo USB do BioMini...");
                    }
                    retry++;
                    Thread.Sleep(1000);
                }

                if (!s_Running) break;

                int hSt = UFS_GetScannerHandle(0, out hScanner);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[✓] SENSOR BIOMINI VINCULADO! Handle: " + hScanner);

                byte[] idBytes = new byte[64];
                UFS_GetScannerID(hScanner, idBytes);
                string serial = Encoding.ASCII.GetString(idBytes).Trim('\0', ' ');
                Console.WriteLine("    Número de Série: " + (string.IsNullOrEmpty(serial) ? "BioMini USB" : serial));
                Console.ResetColor();

                // Configura timeout de captura curta (1500ms) para loop responsivo
                int timeoutVal = 1500;
                UFS_SetParameter(hScanner, UFS_PARAM_TIMEOUT, ref timeoutVal);

                Console.WriteLine("[*] Entrando em ciclo de escuta contínua...");
                Console.WriteLine(">>> POSICIONE O DEDO NO SENSOR FISICO PARA VALIDAR <<<\n");

                int consecutiveErrors = 0;

                while (s_Running) {
                    // Tenta captura rápida de frame
                    int capSt = UFS_CaptureSingleImage(hScanner);

                    if (capSt == 0) { // UFS_OK = 0
                        consecutiveErrors = 0;
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("\n[🖐️ DEDO DETECTADO NO SENSOR!] Processando imagem...");
                        Console.ResetColor();

                        byte[] template = new byte[1024];
                        int templateSize = 0;
                        int quality = 0;

                        int extSt = UFS_Extract(hScanner, template, out templateSize, out quality);
                        if (extSt == 0 && templateSize > 0) {
                            byte[] realTemplate = new byte[templateSize];
                            Array.Copy(template, realTemplate, templateSize);
                            string b64 = Convert.ToBase64String(realTemplate);

                            Console.WriteLine("    -> Minúcias ISO/ANSI extraídas: " + templateSize + " bytes (Qualidade: " + quality + "%)");
                            Console.WriteLine("    -> Disparando autenticação e cupom na Epson...");

                            PostPunch(b64);
                        } else {
                            Console.WriteLine("    -> Erro ao extrair minúcias (código " + extSt + "). Tente posicionar melhor o dedo.");
                        }

                        // Pausa de 2.5 segundos para não disparar múltiplas vezes no mesmo toque
                        Thread.Sleep(2500);
                    } else if (capSt == -1 || capSt == 101 || capSt == 102) {
                        // Timeout normal do sensor aguardando o dedo (esperado no loop)
                        consecutiveErrors = 0;
                        Thread.Sleep(80);
                    } else {
                        // Erro real de hardware / desconexão USB (ex: leitor foi puxado ou travou)
                        consecutiveErrors++;
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("[!] Status do sensor: " + capSt + " (Tentativa " + consecutiveErrors + "/3)");
                        Console.ResetColor();

                        if (consecutiveErrors >= 3) {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("[⚠️ DESCONEXÃO/TRAVAMENTO DETECTADO] Reiniciando ciclo de hardware...");
                            Console.ResetColor();
                            break; // Sai do loop interno para rodar UFS_Uninit e re-inicializar limpo
                        }
                        Thread.Sleep(500);
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("[!] Exceção no daemon: " + ex.Message);
            } finally {
                if (hScanner != IntPtr.Zero) {
                    try { UFS_AbortCapturing(hScanner); } catch {}
                }
                try {
                    Console.WriteLine("[*] Desligando UFS SDK para liberação de memória...");
                    UFS_Uninit();
                } catch {}
                Thread.Sleep(1200);
            }
        }
    }

    private static void PostPunch(string template) {
        try {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://localhost:3300/api/verify");
            req.Method = "POST";
            req.ContentType = "application/json";
            string json = "{\"template\":\"" + template + "\"}";
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            req.ContentLength = bytes.Length;

            using (Stream os = req.GetRequestStream()) {
                os.Write(bytes, 0, bytes.Length);
            }

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse()) {
                using (StreamReader sr = new StreamReader(resp.GetResponseStream())) {
                    string resJson = sr.ReadToEnd();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("    ✓ Retorno Veritas: " + resJson);
                    Console.ResetColor();
                }
            }
        } catch (Exception e) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("    ✗ Falha na API: " + e.Message);
            Console.ResetColor();
        }
    }
}
