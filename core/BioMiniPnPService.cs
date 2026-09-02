using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Suprema;

namespace OpenBioMini.PnP {
    public class BioMiniPnPService : Form {
        // Native PnP Definitions
        private const int WM_DEVICECHANGE = 0x0219;
        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        private const int DBT_DEVTYP_DEVICEINTERFACE = 0x0005;
        private const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x0000;

        private static readonly Guid GUID_DEVINTERFACE_USB_DEVICE = new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED");

        [StructLayout(LayoutKind.Sequential)]
        private struct DEV_BROADCAST_DEVICEINTERFACE {
            public int dbcc_size;
            public int dbcc_devicetype;
            public int dbcc_reserved;
            public Guid dbcc_classguid;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public char[] dbcc_name;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DEV_BROADCAST_HDR {
            public int dbch_size;
            public int dbch_devicetype;
            public int dbch_reserved;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr RegisterDeviceNotification(IntPtr hRecipient, IntPtr NotificationFilter, uint Flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterDeviceNotification(IntPtr Handle);

        // State variables
        private UFScannerManager m_Manager;
        private UFScanner m_Scanner;
        private UFMatcher m_Matcher;
        private IntPtr m_NotifyHandle = IntPtr.Zero;
        private HttpListener m_HttpListener;
        private Thread m_HttpThread;
        private bool m_Running = true;
        private readonly object m_Lock = new object();
        private const int HTTP_PORT = 8080;

        public bool IsConnected {
            get {
                lock (m_Lock) {
                    return m_Scanner != null && m_Manager != null;
                }
            }
        }

        public string ScannerModel {
            get {
                lock (m_Lock) {
                    return m_Scanner != null ? m_Scanner.ScannerType.ToString() : "N/A";
                }
            }
        }

        public string ScannerSerial {
            get {
                lock (m_Lock) {
                    return m_Scanner != null ? m_Scanner.Serial : "N/A";
                }
            }
        }

        [STAThread]
        public static void Main(string[] args) {
            Console.Title = "OpenBioMini PnP Watchdog & REST Bridge v3.0";
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("==================================================");
            Console.WriteLine("🛡️  OPEN-BIOMINI UNIVERSAL PNP WATCHDOG v3.0");
            Console.WriteLine("   Hot-Plug USB Engine • Auto-Recovery • REST API");
            Console.WriteLine("==================================================\n");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new BioMiniPnPService());
        }

        public BioMiniPnPService() {
            // Form oculto apenas para captura de mensagens nativas do Windows
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(0, 0);

            this.Load += (s, e) => {
                this.Hide();
                RegisterUsbNotification();
                StartHttpServer();
                TryConnectScanner("Inicialização do Sistema");
            };

            this.FormClosing += (s, e) => {
                m_Running = false;
                if (m_NotifyHandle != IntPtr.Zero) {
                    try { UnregisterDeviceNotification(m_NotifyHandle); } catch { }
                }
                StopHttpServer();
                DisconnectScanner("Encerramento do Serviço");
            };
        }

        private void RegisterUsbNotification() {
            try {
                DEV_BROADCAST_DEVICEINTERFACE dbi = new DEV_BROADCAST_DEVICEINTERFACE();
                dbi.dbcc_size = Marshal.SizeOf(dbi);
                dbi.dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE;
                dbi.dbcc_classguid = GUID_DEVINTERFACE_USB_DEVICE;
                dbi.dbcc_name = new char[256];

                IntPtr pBuffer = Marshal.AllocHGlobal(dbi.dbcc_size);
                Marshal.StructureToPtr(dbi, pBuffer, true);

                m_NotifyHandle = RegisterDeviceNotification(this.Handle, pBuffer, DEVICE_NOTIFY_WINDOW_HANDLE);
                Marshal.FreeHGlobal(pBuffer);

                Console.WriteLine("[*] Registro de notificações PnP USB ativo (HWND: " + this.Handle + ")");
            } catch (Exception ex) {
                Console.WriteLine("[!] Erro ao registrar notificações USB: " + ex.Message);
            }
        }

        protected override void WndProc(ref Message m) {
            if (m.Msg == WM_DEVICECHANGE) {
                int eventType = m.WParam.ToInt32();

                if (eventType == DBT_DEVICEARRIVAL) {
                    string devPath = GetDevicePathFromLParam(m.LParam);
                    if (string.IsNullOrEmpty(devPath) || devPath.ToLower().Contains("16d1") || devPath.ToLower().Contains("sfr")) {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("\n[🔌 USB CONECTADO] Suprema BioMini detectado na porta USB!");
                        Console.ResetColor();

                        // Thread com debounce de 350ms para estabilização da porta USB
                        ThreadPool.QueueUserWorkItem((state) => {
                            Thread.Sleep(350);
                            this.Invoke(new Action(() => {
                                TryConnectScanner("Hot-Plug Conexão");
                            }));
                        });
                    }
                } else if (eventType == DBT_DEVICEREMOVECOMPLETE) {
                    string devPath = GetDevicePathFromLParam(m.LParam);
                    if (string.IsNullOrEmpty(devPath) || devPath.ToLower().Contains("16d1") || devPath.ToLower().Contains("sfr")) {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("\n[🔌 USB DESCONECTADO] Suprema BioMini removido do computador.");
                        Console.ResetColor();

                        this.Invoke(new Action(() => {
                            DisconnectScanner("Hot-Plug Desconexão Física");
                        }));
                    }
                }
            }

            base.WndProc(ref m);
        }

        private string GetDevicePathFromLParam(IntPtr lParam) {
            if (lParam == IntPtr.Zero) return null;
            try {
                DEV_BROADCAST_HDR hdr = (DEV_BROADCAST_HDR)Marshal.PtrToStructure(lParam, typeof(DEV_BROADCAST_HDR));
                if (hdr.dbch_devicetype == DBT_DEVTYP_DEVICEINTERFACE) {
                    IntPtr pName = new IntPtr(lParam.ToInt64() + 28);
                    return Marshal.PtrToStringAuto(pName);
                }
            } catch { }
            return null;
        }

        public bool TryConnectScanner(string reason) {
            lock (m_Lock) {
                try {
                    Console.WriteLine("[*] Conectando leitor biométrico (" + reason + ")...");

                    if (m_Manager != null) {
                        try { m_Manager.Uninit(); } catch { }
                        m_Manager = null;
                        m_Scanner = null;
                    }

                    m_Manager = new UFScannerManager(this);
                    UFS_STATUS initStatus = m_Manager.Init();

                    if (initStatus != UFS_STATUS.OK) {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("    [-] SDK Init retornou status: " + initStatus);
                        Console.ResetColor();
                        return false;
                    }

                    m_Matcher = new UFMatcher();

                    for (int i = 0; i < 10; i++) {
                        Application.DoEvents();
                        Thread.Sleep(80);
                        m_Manager.Update();
                        if (m_Manager.Scanners.Count > 0) break;
                    }

                    if (m_Manager.Scanners.Count > 0) {
                        m_Scanner = m_Manager.Scanners[0];
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("    [✓] SCANNER VINCULADO COM SUCESSO!");
                        Console.WriteLine("        Modelo: " + m_Scanner.ScannerType);
                        Console.WriteLine("        Serial: " + (string.IsNullOrEmpty(m_Scanner.Serial) ? "BioMini USB" : m_Scanner.Serial));
                        Console.ResetColor();

                        // Notifica backend Veritas
                        NotifyVeritasStatus(true);
                        return true;
                    } else {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine("    [-] Scanner em standby (aguardando inserção USB).");
                        Console.ResetColor();
                        NotifyVeritasStatus(false);
                        return false;
                    }
                } catch (Exception ex) {
                    Console.WriteLine("    [!] Exceção ao conectar scanner: " + ex.Message);
                    NotifyVeritasStatus(false);
                    return false;
                }
            }
        }

        public void DisconnectScanner(string reason) {
            lock (m_Lock) {
                try {
                    Console.WriteLine("[*] Liberando recursos do leitor (" + reason + ")...");
                    if (m_Scanner != null) {
                        try { m_Scanner.AbortCapturing(); } catch { }
                        m_Scanner = null;
                    }
                    if (m_Manager != null) {
                        try { m_Manager.Uninit(); } catch { }
                        m_Manager = null;
                    }
                    m_Matcher = null;
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("    [✓] Driver liberado limpo pelo Uninit.");
                    Console.ResetColor();
                    NotifyVeritasStatus(false);
                } catch (Exception ex) {
                    Console.WriteLine("    [!] Erro na desconexão: " + ex.Message);
                }
            }
        }

        public ScanResult Capture(int timeoutMs) {
            lock (m_Lock) {
                ScanResult result = new ScanResult();

                if (!IsConnected) {
                    // Tenta reconectar caso o leitor tenha sido plugado
                    if (!TryConnectScanner("Tentativa de Captura sob Demanda")) {
                        result.ErrorMessage = "Leitor biométrico desconectado da USB.";
                        return result;
                    }
                }

                try {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("\n[🖐️ CAPTURA SOLICITADA] Acendendo prisma óptico...");
                    Console.ResetColor();

                    m_Scanner.Timeout = timeoutMs;
                    UFS_STATUS status = m_Scanner.CaptureSingleImage();

                    if (status != UFS_STATUS.OK) {
                        result.ErrorMessage = "Status da captura: " + status;
                        Console.WriteLine("    [-] Falha na captura: " + status);
                        return result;
                    }

                    Bitmap bmp = null;
                    int res = 0;
                    m_Scanner.GetCaptureImageBuffer(out bmp, out res);

                    if (bmp != null) {
                        result.ImageBitmap = bmp;
                        using (MemoryStream ms = new MemoryStream()) {
                            bmp.Save(ms, ImageFormat.Png);
                            result.ImageBase64 = Convert.ToBase64String(ms.ToArray());
                        }
                    }

                    byte[] template = new byte[1024];
                    int templateSize = 0;
                    int quality = 0;

                    UFS_STATUS extStatus = m_Scanner.Extract(template, out templateSize, out quality);
                    if (extStatus == UFS_STATUS.OK && templateSize > 0) {
                        result.Success = true;
                        result.Template = new byte[templateSize];
                        Array.Copy(template, result.Template, templateSize);
                        result.TemplateSize = templateSize;
                        result.Quality = quality;

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("    [✓] DIGITAL CAPTURADA COM SUCESSO!");
                        Console.WriteLine("        Minúcias: " + templateSize + " bytes | Qualidade: " + quality + "%");
                        Console.ResetColor();
                    } else {
                        result.ErrorMessage = "Falha ao extrair minúcias: " + extStatus;
                        Console.WriteLine("    [-] Falha ao extrair minúcias: " + extStatus);
                    }
                } catch (Exception ex) {
                    result.ErrorMessage = "Exceção durante captura: " + ex.Message;
                    Console.WriteLine("    [!] Exceção de captura: " + ex.Message);
                }

                return result;
            }
        }

        private void StartHttpServer() {
            try {
                m_HttpListener = new HttpListener();
                m_HttpListener.Prefixes.Add("http://localhost:" + HTTP_PORT + "/");
                m_HttpListener.Prefixes.Add("http://127.0.0.1:" + HTTP_PORT + "/");
                m_HttpListener.Start();

                m_HttpThread = new Thread(() => {
                    while (m_Running) {
                        try {
                            HttpListenerContext ctx = m_HttpListener.GetContext();
                            ThreadPool.QueueUserWorkItem((state) => HandleHttpRequest(ctx));
                        } catch {
                            if (!m_Running) break;
                        }
                    }
                });
                m_HttpThread.IsBackground = true;
                m_HttpThread.Start();

                Console.WriteLine("[*] REST API PnP ativa em http://localhost:" + HTTP_PORT + "/");
            } catch (Exception ex) {
                Console.WriteLine("[!] Erro ao iniciar servidor HTTP: " + ex.Message);
            }
        }

        private void StopHttpServer() {
            try {
                if (m_HttpListener != null) {
                    m_HttpListener.Stop();
                    m_HttpListener.Close();
                }
            } catch { }
        }

        private void HandleHttpRequest(HttpListenerContext ctx) {
            HttpListenerRequest req = ctx.Request;
            HttpListenerResponse res = ctx.Response;

            res.AddHeader("Access-Control-Allow-Origin", "*");
            res.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            res.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");

            if (req.HttpMethod == "OPTIONS") {
                res.StatusCode = 200;
                res.Close();
                return;
            }

            string path = req.Url.AbsolutePath.ToLower();
            string responseJson = "{}";

            try {
                if (path == "/api/status" || path == "/api/") {
                    bool connected = IsConnected;
                    responseJson = string.Format(
                        "{{\"connected\":{0},\"model\":\"{1}\",\"serial\":\"{2}\",\"pnpReady\":true}}",
                        connected ? "true" : "false",
                        ScannerModel,
                        ScannerSerial
                    );
                } else if (path == "/api/scan" || path == "/api/capture") {
                    ScanResult resObj = Capture(6000);
                    if (resObj.Success) {
                        string b64 = Convert.ToBase64String(resObj.Template);
                        responseJson = string.Format(
                            "{{\"success\":true,\"quality\":{0},\"templateSize\":{1},\"template\":\"{2}\",\"imageBase64\":\"{3}\"}}",
                            resObj.Quality,
                            resObj.TemplateSize,
                            b64,
                            resObj.ImageBase64 ?? ""
                        );
                    } else {
                        responseJson = string.Format("{{\"success\":false,\"error\":\"{0}\"}}", resObj.ErrorMessage ?? "Falha");
                    }
                } else {
                    res.StatusCode = 404;
                    responseJson = "{\"error\":\"Rota inexistente\"}";
                }
            } catch (Exception ex) {
                res.StatusCode = 500;
                responseJson = string.Format("{{\"error\":\"{0}\"}}", ex.Message.Replace("\"", "'"));
            }

            byte[] outBuf = Encoding.UTF8.GetBytes(responseJson);
            res.ContentType = "application/json; charset=utf-8";
            res.ContentLength64 = outBuf.Length;
            res.OutputStream.Write(outBuf, 0, outBuf.Length);
            res.Close();
        }

        private void NotifyVeritasStatus(bool online) {
            try {
                ThreadPool.QueueUserWorkItem((state) => {
                    try {
                        HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://localhost:3300/api/hardware-status-update");
                        req.Method = "POST";
                        req.ContentType = "application/json";
                        req.Timeout = 800;
                        string json = "{\"biomini\":" + (online ? "true" : "false") + "}";
                        byte[] b = Encoding.UTF8.GetBytes(json);
                        req.ContentLength = b.Length;
                        using (Stream s = req.GetRequestStream()) { s.Write(b, 0, b.Length); }
                        using (req.GetResponse()) { }
                    } catch { }
                });
            } catch { }
        }
    }

    public class ScanResult {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public byte[] Template { get; set; }
        public int TemplateSize { get; set; }
        public int Quality { get; set; }
        public string ImageBase64 { get; set; }
        public Bitmap ImageBitmap { get; set; }
    }
}
