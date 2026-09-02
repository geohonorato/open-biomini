using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Suprema;

public class TouchService : Form {
    private UFScannerManager m_Manager;
    private UFScanner m_Scanner;
    private bool m_Running = true;
    private Thread m_WorkerThread;

    public TouchService() {
        this.WindowState = FormWindowState.Minimized;
        this.ShowInTaskbar = false;
        this.Load += (s, e) => {
            this.Hide();
            StartService();
        };
    }

    private void StartService() {
        m_WorkerThread = new Thread(RunLoop);
        m_WorkerThread.IsBackground = true;
        m_WorkerThread.Start();
    }

    private void RunLoop() {
        Console.WriteLine("==================================================");
        Console.WriteLine("🖐️ BIOMINI CONTINUOUS TOUCH & PUNCH ENGINE");
        Console.WriteLine("==================================================");

        try {
            m_Manager = new UFScannerManager(this);
            UFS_STATUS st = m_Manager.Init();
            Console.WriteLine("[*] Suprema SDK Manager Init: " + st);

            while (m_Running) {
                try {
                    this.Invoke(new Action(() => {
                        m_Manager.Update();
                    }));

                    if (m_Manager.Scanners.Count > 0) {
                        m_Scanner = m_Manager.Scanners[0];
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("[+] Sensor BioMini conectado! Loop de escuta de toque armado.");
                        Console.ResetColor();

                        while (m_Running && m_Scanner != null) {
                            m_Scanner.Timeout = 1500;
                            UFS_STATUS cap = m_Scanner.CaptureSingleImage();

                            if (cap == UFS_STATUS.OK) {
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                Console.WriteLine("\n[🖐️ TOQUE NO SENSOR DETECTADO!] Captura realizada com sucesso!");
                                Console.ResetColor();

                                byte[] template = new byte[1024];
                                int size = 0, quality = 0;
                                UFS_STATUS ext = m_Scanner.Extract(template, out size, out quality);

                                if (ext == UFS_STATUS.OK && size > 0) {
                                    byte[] realTpl = new byte[size];
                                    Array.Copy(template, realTpl, size);
                                    string base64Tpl = Convert.ToBase64String(realTpl);

                                    Console.WriteLine("    -> Minúcias ISO/ANSI extraídas: " + size + " bytes (Qualidade: " + quality + "%)");
                                    Console.WriteLine("    -> Disparando autenticação e cupom no Veritas...");

                                    // Envia para o servidor Express local na porta 3300
                                    PostPunch(base64Tpl);
                                } else {
                                    Console.WriteLine("    -> Aviso ao extrair minúcias: " + ext);
                                }

                                // Cooldown para evitar disparos repetidos com o mesmo dedo pousado
                                Thread.Sleep(3000);
                            } else {
                                Thread.Sleep(150);
                            }
                        }
                    } else {
                        Thread.Sleep(2000);
                    }
                } catch (Exception ex) {
                    Console.WriteLine("[!] Aviso no ciclo de escuta: " + ex.Message);
                    Thread.Sleep(2000);
                }
            }
        } catch (Exception e) {
            Console.WriteLine("[!] Erro fatal na inicialização: " + e.Message);
        }
    }

    private void PostPunch(string template) {
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
                    Console.WriteLine("    ✓ Retorno do Veritas: " + resJson);
                    Console.ResetColor();
                }
            }
        } catch (Exception e) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("    ✗ Falha ao enviar para o Veritas: " + e.Message);
            Console.ResetColor();
        }
    }

    [STAThread]
    static void Main() {
        Application.Run(new TouchService());
    }
}
