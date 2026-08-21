using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using OpenBioMini;

namespace OpenBioMini.Bridge {
    class Program {
        private static BioMiniController s_Controller;
        private static HttpListener s_Listener;
        private static bool s_Running = true;
        private const int PORT = 8080;

        static void Main(string[] args) {
            Console.Title = "OpenBioMini — HTTP REST Bridge Server";
            Console.WriteLine("==================================================");
            Console.WriteLine("🚀 OPEN-BIOMINI HTTP REST BRIDGE SERVER");
            Console.WriteLine("   Local Endpoint: http://localhost:" + PORT + "/api/");
            Console.WriteLine("==================================================\n");

            Console.Write("[*] Inicializando leitor biométrico... ");
            s_Controller = new BioMiniController();
            if (s_Controller.Initialize()) {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("OK!");
                Console.ResetColor();
                Console.WriteLine("    Modelo: " + s_Controller.ScannerModel);
                Console.WriteLine("    Serial: " + s_Controller.ScannerSerial);
            } else {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("AVISO: Leitor em standby (verifique o cabo USB).");
                Console.ResetColor();
            }

            try {
                s_Listener = new HttpListener();
                s_Listener.Prefixes.Add("http://localhost:" + PORT + "/");
                s_Listener.Prefixes.Add("http://127.0.0.1:" + PORT + "/");
                s_Listener.Start();
                Console.WriteLine("\n[*] Servidor HTTP escutando em http://localhost:" + PORT + "/");
                Console.WriteLine("[*] Pressione Ctrl+C para encerrar.\n");

                while (s_Running) {
                    try {
                        HttpListenerContext ctx = s_Listener.GetContext();
                        ThreadPool.QueueUserWorkItem((state) => HandleRequest(ctx));
                    } catch {
                        if (!s_Running) break;
                    }
                }
            } catch (Exception ex) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[!] Erro ao iniciar servidor HTTP: " + ex.Message);
                Console.ResetColor();
            } finally {
                if (s_Controller != null) s_Controller.Dispose();
            }
        }

        private static void HandleRequest(HttpListenerContext ctx) {
            HttpListenerRequest req = ctx.Request;
            HttpListenerResponse res = ctx.Response;

            // Habilita CORS total para WebApps e SPAs locais
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
                    bool connected = s_Controller.IsConnected;
                    if (!connected) {
                        connected = s_Controller.Initialize();
                    }

                    responseJson = string.Format(
                        "{{\"connected\":{0},\"model\":\"{1}\",\"serial\":\"{2}\",\"version\":\"1.0.0\"}}",
                        connected ? "true" : "false",
                        s_Controller.ScannerModel,
                        s_Controller.ScannerSerial
                    );
                    Console.WriteLine("[GET] /api/status -> Conectado: " + connected);
                }
                else if (path == "/api/scan" || path == "/api/capture") {
                    Console.WriteLine("[POST] /api/scan -> Aguardando dedo no sensor...");
                    if (!s_Controller.IsConnected) {
                        s_Controller.Initialize();
                    }

                    ScanResult result = s_Controller.Capture(6000);

                    if (result.Success) {
                        string templateB64 = Convert.ToBase64String(result.Template);
                        responseJson = string.Format(
                            "{{\"success\":true,\"quality\":{0},\"templateSize\":{1},\"template\":\"{2}\",\"imageBase64\":\"{3}\"}}",
                            result.Quality,
                            result.TemplateSize,
                            templateB64,
                            result.ImageBase64 ?? ""
                        );
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("       ✓ Captura concluída! Qualidade: " + result.Quality + "%");
                        Console.ResetColor();
                    } else {
                        responseJson = string.Format(
                            "{{\"success\":false,\"error\":\"{0}\"}}",
                            result.ErrorMessage ?? "Falha na captura"
                        );
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("       ✗ " + result.ErrorMessage);
                        Console.ResetColor();
                    }
                }
                else if (path == "/api/match" || path == "/api/verify") {
                    string body = "";
                    using (StreamReader reader = new StreamReader(req.InputStream, req.ContentEncoding)) {
                        body = reader.ReadToEnd();
                    }

                    // Parser JSON simplificado sem dependências externas
                    string tA = ExtractJsonString(body, "templateA");
                    string tB = ExtractJsonString(body, "templateB");

                    if (!string.IsNullOrEmpty(tA) && !string.IsNullOrEmpty(tB)) {
                        byte[] bytesA = Convert.FromBase64String(tA);
                        byte[] bytesB = Convert.FromBase64String(tB);
                        bool isMatch = s_Controller.Verify(bytesA, bytesA.Length, bytesB, bytesB.Length);

                        responseJson = string.Format("{{\"match\":{0}}}", isMatch ? "true" : "false");
                        Console.WriteLine("[POST] /api/match -> Match: " + isMatch);
                    } else {
                        responseJson = "{\"match\":false,\"error\":\"Parâmetros templateA ou templateB ausentes\"}";
                    }
                }
                else {
                    res.StatusCode = 404;
                    responseJson = "{\"error\":\"Rota não encontrada\"}";
                }
            } catch (Exception ex) {
                res.StatusCode = 500;
                responseJson = string.Format("{{\"error\":\"{0}\"}}", ex.Message.Replace("\"", "'"));
            }

            byte[] buffer = Encoding.UTF8.GetBytes(responseJson);
            res.ContentType = "application/json; charset=utf-8";
            res.ContentLength64 = buffer.Length;
            res.OutputStream.Write(buffer, 0, buffer.Length);
            res.Close();
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
    }
}
