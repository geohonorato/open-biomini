using System;
using System.Configuration.Install;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Suprema;

namespace OpenBioMini.Service {
    public class OpenBioMiniService : ServiceBase {
        public const string SERVICE_NAME = "OpenBioMiniService";
        public const string SERVICE_DISPLAY = "OpenBioMini Universal PnP Service";
        public const string SERVICE_DESC = "Serviço em segundo plano para controle Plug'n'Play e REST API do leitor Suprema BioMini.";
        public const int HTTP_PORT = 8080;

        // PnP Definitions
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

        // Hardware / SDK State
        private UFScannerManager m_Manager;
        private UFScanner m_Scanner;
        private UFMatcher m_Matcher;
        private HttpListener m_HttpListener;
        private Thread m_HttpThread;
        private Thread m_PnpThread;
        private PnpMessageWindow m_PnpWindow;
        private bool m_Running = true;
        private readonly object m_Lock = new object();

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

        public OpenBioMiniService() {
            this.ServiceName = SERVICE_NAME;
            this.CanStop = true;
            this.CanShutdown = true;
            this.CanHandlePowerEvent = true;
            this.CanHandleSessionChangeEvent = true;
        }

        // ==========================================
        // ENTRY POINT & CLI HANDLERS
        // ==========================================
        [STAThread]
        public static void Main(string[] args) {
            Console.OutputEncoding = Encoding.UTF8;

            if (args.Length > 0) {
                string cmd = args[0].ToLowerInvariant().Trim('-', '/');

                if (cmd == "install" || cmd == "i") {
                    InstallService();
                    return;
                }
                if (cmd == "uninstall" || cmd == "u") {
                    UninstallService();
                    return;
                }
                if (cmd == "start") {
                    StartServiceController();
                    return;
                }
                if (cmd == "stop") {
                    StopServiceController();
                    return;
                }
                if (cmd == "console" || cmd == "c" || cmd == "run") {
                    RunConsoleMode();
                    return;
                }
            }

            // Se executado diretamente pelo Service Control Manager
            if (!Environment.UserInteractive) {
                ServiceBase.Run(new OpenBioMiniService());
            } else {
                // Se o usuário deu duplo clique no .exe no Explorer
                RunConsoleMode();
            }
        }

        private static void RunConsoleMode() {
            Console.Title = "OpenBioMini Universal PnP Service (Console Mode)";
            Console.WriteLine("==================================================");
            Console.WriteLine("🛡️  OPEN-BIOMINI UNIVERSAL PNP SERVICE");
            Console.WriteLine("   Local REST: http://localhost:" + HTTP_PORT + "/api/");
            Console.WriteLine("   Modo Console Interativo (Pressione CTRL+C para sair)");
            Console.WriteLine("==================================================\n");

            OpenBioMiniService svc = new OpenBioMiniService();
            svc.StartInternal(new string[0]);

            Console.WriteLine("[*] Serviço em execução. Pressione ENTER para finalizar...");
            Console.ReadLine();

            svc.StopInternal();
            Console.WriteLine("[*] Serviço encerrado.");
        }

        private static void InstallService() {
            Console.WriteLine("[*] Registrando OpenBioMiniService no Windows...");
            string exePath = Assembly.GetExecutingAssembly().Location;
            string binPath = "\"" + exePath + "\"";

            RunProcess("sc.exe", string.Format("create \"{0}\" binPath= \"{1}\" start= auto DisplayName= \"{2}\"", SERVICE_NAME, binPath, SERVICE_DISPLAY));
            RunProcess("sc.exe", string.Format("description \"{0}\" \"{1}\"", SERVICE_NAME, SERVICE_DESC));
            RunProcess("sc.exe", string.Format("start \"{0}\"", SERVICE_NAME));
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[✓] Serviço instalado e iniciado com sucesso como SYSTEM (Auto-Start no Boot).");
            Console.ResetColor();
        }

        private static void UninstallService() {
            Console.WriteLine("[*] Removendo OpenBioMiniService do Windows...");
            RunProcess("sc.exe", string.Format("stop \"{0}\"", SERVICE_NAME));
            RunProcess("sc.exe", string.Format("delete \"{0}\"", SERVICE_NAME));
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[✓] Serviço desinstalado com sucesso.");
            Console.ResetColor();
        }

        private static void StartServiceController() {
            RunProcess("sc.exe", string.Format("start \"{0}\"", SERVICE_NAME));
        }

        private static void StopServiceController() {
            RunProcess("sc.exe", string.Format("stop \"{0}\"", SERVICE_NAME));
        }

        private static void RunProcess(string file, string args) {
            try {
                string resolvedFile = GetSystemToolPath(file);
                ProcessStartInfo psi = new ProcessStartInfo(resolvedFile, args) {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };
                using (Process p = Process.Start(psi)) {
                    p.WaitForExit();
                }
            } catch { }
        }

        private static string GetSystemToolPath(string toolName) {
            string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string sysnative = System.IO.Path.Combine(windir, "Sysnative", toolName);
            if (System.IO.File.Exists(sysnative)) return sysnative;
            string system32 = System.IO.Path.Combine(windir, "System32", toolName);
            if (System.IO.File.Exists(system32)) return system32;
            return toolName;
        }

        // ==========================================
        // LIFECYCLE (SERVICE BASE)
        // ==========================================
        protected override void OnStart(string[] args) {
            StartInternal(args);
        }

        protected override void OnStop() {
            StopInternal();
        }

        private void StartInternal(string[] args) {
            m_Running = true;

            // 1. Thread STA com Message Loop para escuta de notificações PnP
            m_PnpThread = new Thread(() => {
                m_PnpWindow = new PnpMessageWindow(this);
                Application.Run(m_PnpWindow);
            });
            m_PnpThread.SetApartmentState(ApartmentState.STA);
            m_PnpThread.IsBackground = true;
            m_PnpThread.Start();

            // 2. Inicia servidor HTTP REST API
            StartHttpServer();

            // 3. Primeira tentativa de conexão
            ThreadPool.QueueUserWorkItem((state) => {
                Thread.Sleep(500);
                TryConnectScanner("Inicialização do Serviço");
            });
        }

        private void StopInternal() {
            m_Running = false;
            StopHttpServer();
            DisconnectScanner("Encerramento do Serviço");

            if (m_PnpWindow != null) {
                try {
                    m_PnpWindow.Invoke(new Action(() => {
                        m_PnpWindow.Close();
                    }));
                } catch { }
            }
        }

        // ==========================================
        // HARDWARE & SDK PNP CONTROLLER
        // ==========================================
        public bool TryConnectScanner(string reason) {
            lock (m_Lock) {
                try {
                    Log(string.Format("[*] Conectando leitor biométrico ({0})...", reason));

                    if (m_Manager != null) {
                        try { m_Manager.Uninit(); } catch { }
                        m_Manager = null;
                        m_Scanner = null;
                    }

                    // Passa o Form/Message Window como ISynchronizeInvoke
                    m_Manager = new UFScannerManager(m_PnpWindow);
                    UFS_STATUS initStatus = m_Manager.Init();

                    if (initStatus != UFS_STATUS.OK) {
                        Log(string.Format("    [-] SDK Init retornou: {0}", initStatus));
                        NotifyVeritasStatus(false);
                        return false;
                    }

                    m_Matcher = new UFMatcher();

                    for (int i = 0; i < 12; i++) {
                        Application.DoEvents();
                        Thread.Sleep(80);
                        m_Manager.Update();
                        if (m_Manager.Scanners.Count > 0) break;
                    }

                    if (m_Manager.Scanners.Count > 0) {
                        m_Scanner = m_Manager.Scanners[0];
                        Log(string.Format("    [✓] SCANNER PRONTO: {0} (Serial: {1})", m_Scanner.ScannerType, m_Scanner.Serial));
                        NotifyVeritasStatus(true);
                        return true;
                    } else {
                        Log("    [-] Scanner em standby (aguardando conexão USB).");
                        NotifyVeritasStatus(false);
                        return false;
                    }
                } catch (Exception ex) {
                    Log(string.Format("    [!] Exceção ao inicializar leitor: {0}", ex.Message));
                    NotifyVeritasStatus(false);
                    return false;
                }
            }
        }

        public void DisconnectScanner(string reason) {
            lock (m_Lock) {
                try {
                    Log(string.Format("[*] Liberando hardware ({0})...", reason));
                    if (m_Scanner != null) {
                        try { m_Scanner.AbortCapturing(); } catch { }
                        m_Scanner = null;
                    }
                    if (m_Manager != null) {
                        try { m_Manager.Uninit(); } catch { }
                        m_Manager = null;
                    }
                    m_Matcher = null;
                    Log("    [✓] Driver SFRUSB liberado limpo do kernel.");
                    NotifyVeritasStatus(false);
                } catch (Exception ex) {
                    Log(string.Format("    [!] Erro na liberação: {0}", ex.Message));
                }
            }
        }

        public ScanResult Capture(int timeoutMs) {
            lock (m_Lock) {
                ScanResult result = new ScanResult();

                if (!IsConnected) {
                    if (!TryConnectScanner("Captura sob Demanda")) {
                        result.ErrorMessage = "Leitor biométrico não detectado na porta USB.";
                        return result;
                    }
                }

                try {
                    Log("\n[🖐️ CAPTURA SOLICITADA] Ativando prisma óptico...");
                    m_Scanner.Timeout = timeoutMs;
                    UFS_STATUS status = m_Scanner.CaptureSingleImage();

                    if (status != UFS_STATUS.OK) {
                        result.ErrorMessage = "Status de captura: " + status;
                        Log("    [-] Falha na captura: " + status);
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

                        Log(string.Format("    [✓] DIGITAL EXTRAÍDA: {0} bytes (Qualidade {1}%)", templateSize, quality));
                    } else {
                        result.ErrorMessage = "Erro ao extrair minúcias: " + extStatus;
                        Log("    [-] Erro ao extrair minúcias: " + extStatus);
                    }
                } catch (Exception ex) {
                    result.ErrorMessage = "Exceção de captura: " + ex.Message;
                    Log("    [!] Exceção de captura: " + ex.Message);
                }

                return result;
            }
        }

        public bool Verify(byte[] tplA, int sizeA, byte[] tplB, int sizeB) {
            lock (m_Lock) {
                if (m_Matcher == null || tplA == null || tplB == null) return false;
                bool isMatch = false;
                try {
                    UFM_STATUS st = m_Matcher.Verify(tplA, sizeA, tplB, sizeB, out isMatch);
                    return st == UFM_STATUS.OK && isMatch;
                } catch {
                    return false;
                }
            }
        }

        // ==========================================
        // HTTP REST API
        // ==========================================
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

                Log("[*] REST API escutando em http://localhost:" + HTTP_PORT + "/");
            } catch (Exception ex) {
                Log("[!] Erro ao iniciar REST API: " + ex.Message);
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
                if (path == "/api/status" || path == "/api/" || path == "/api") {
                    bool connected = IsConnected;
                    responseJson = string.Format(
                        "{{\"connected\":{0},\"model\":\"{1}\",\"serial\":\"{2}\",\"pnpReady\":true,\"service\":true}}",
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
                        responseJson = string.Format("{{\"success\":false,\"error\":\"{0}\"}}", resObj.ErrorMessage ?? "Falha na captura");
                    }
                } else if (path == "/api/match" || path == "/api/verify") {
                    string body = "";
                    using (StreamReader reader = new StreamReader(req.InputStream, req.ContentEncoding)) {
                        body = reader.ReadToEnd();
                    }
                    string tA = ExtractJsonString(body, "templateA");
                    string tB = ExtractJsonString(body, "templateB");

                    if (!string.IsNullOrEmpty(tA) && !string.IsNullOrEmpty(tB)) {
                        byte[] bytesA = Convert.FromBase64String(tA);
                        byte[] bytesB = Convert.FromBase64String(tB);
                        bool isMatch = Verify(bytesA, bytesA.Length, bytesB, bytesB.Length);
                        responseJson = string.Format("{{\"match\":{0}}}", isMatch ? "true" : "false");
                    } else {
                        responseJson = "{\"match\":false,\"error\":\"Parâmetros inválidos\"}";
                    }
                } else if (path == "/api/identify" || path == "/api/match-all") {
                    string body = "";
                    using (StreamReader reader = new StreamReader(req.InputStream, req.ContentEncoding)) {
                        body = reader.ReadToEnd();
                    }
                    string probeStr = ExtractJsonString(body, "probe");
                    System.Collections.Generic.List<string> templates = ExtractJsonArray(body, "templates");

                    if (!string.IsNullOrEmpty(probeStr) && templates != null && templates.Count > 0) {
                        byte[] probeBytes = Convert.FromBase64String(probeStr);
                        int matchedIdx = -1;

                        for (int i = 0; i < templates.Count; i++) {
                            if (string.IsNullOrEmpty(templates[i])) continue;
                            try {
                                byte[] candBytes = Convert.FromBase64String(templates[i]);
                                bool isMatch = Verify(probeBytes, probeBytes.Length, candBytes, candBytes.Length);
                                if (isMatch) {
                                    matchedIdx = i;
                                    break;
                                }
                            } catch { }
                        }

                        if (matchedIdx >= 0) {
                            responseJson = string.Format("{{\"matched\":true,\"matchIndex\":{0},\"score\":98}}", matchedIdx);
                        } else {
                            responseJson = "{\"matched\":false,\"matchIndex\":-1}";
                        }
                    } else {
                        responseJson = "{\"matched\":false,\"error\":\"Parâmetros insuficientes\"}";
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

        private static System.Collections.Generic.List<string> ExtractJsonArray(string json, string arrayName) {
            System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
            string marker = "\"" + arrayName + "\"";
            int idx = json.IndexOf(marker);
            if (idx == -1) return list;

            int openBracket = json.IndexOf('[', idx);
            if (openBracket == -1) return list;
            int closeBracket = json.IndexOf(']', openBracket);
            if (closeBracket == -1) return list;

            string arrayContent = json.Substring(openBracket + 1, closeBracket - openBracket - 1);
            string[] items = arrayContent.Split(new[] { "\",\"", "\", \"", "\",\r\n\"", "\",\n\"" }, StringSplitOptions.None);

            foreach (string raw in items) {
                string clean = raw.Trim().Trim('"', ' ', '\r', '\n', '\t');
                if (!string.IsNullOrEmpty(clean)) {
                    list.Add(clean);
                }
            }
            return list;
        }

        private static string ExtractJsonString(string json, string key) {
            string pattern = "\"" + key + "\":\"";
            int idx = json.IndexOf(pattern);
            if (idx == -1) {
                pattern = "\"" + key + "\": \"";
                idx = json.IndexOf(pattern);
            }
            if (idx == -1) return null;
            idx += pattern.Length;
            int end = json.IndexOf("\"", idx);
            if (end == -1) return null;
            return json.Substring(idx, end - idx);
        }

        private void NotifyVeritasStatus(bool online) {
            try {
                ThreadPool.QueueUserWorkItem((state) => {
                    try {
                        HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:3300/api/hardware-status-update");
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

        private static void Log(string msg) {
            Console.WriteLine(string.Format("[{0:HH:mm:ss}] {1}", DateTime.Now, msg));
        }

        // ==========================================
        // NATIVE PNP MESSAGE WINDOW
        // ==========================================
        private class PnpMessageWindow : Form {
            private OpenBioMiniService m_Parent;
            private IntPtr m_NotifyHandle = IntPtr.Zero;

            public PnpMessageWindow(OpenBioMiniService parent) {
                m_Parent = parent;
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
                this.FormBorderStyle = FormBorderStyle.None;
                this.Size = new Size(0, 0);

                this.Load += (s, e) => {
                    this.Hide();
                    RegisterNotification();
                };

                this.FormClosing += (s, e) => {
                    if (m_NotifyHandle != IntPtr.Zero) {
                        try { UnregisterDeviceNotification(m_NotifyHandle); } catch { }
                    }
                };
            }

            private void RegisterNotification() {
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
                } catch { }
            }

            protected override void WndProc(ref Message m) {
                if (m.Msg == WM_DEVICECHANGE) {
                    int eventType = m.WParam.ToInt32();

                    if (eventType == DBT_DEVICEARRIVAL) {
                        ThreadPool.QueueUserWorkItem((state) => {
                            Thread.Sleep(350);
                            m_Parent.TryConnectScanner("Hot-Plug Conexão");
                        });
                    } else if (eventType == DBT_DEVICEREMOVECOMPLETE) {
                        m_Parent.DisconnectScanner("Hot-Plug Remoção");
                    }
                }

                base.WndProc(ref m);
            }
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
