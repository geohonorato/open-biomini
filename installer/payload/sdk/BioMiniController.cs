using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Suprema;

namespace OpenBioMini {
    public class ScanResult {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public byte[] Template { get; set; }
        public int TemplateSize { get; set; }
        public int Quality { get; set; }
        public string ImageBase64 { get; set; }
        public Bitmap ImageBitmap { get; set; }
    }

    public class BioMiniController : IDisposable {
        private UFScannerManager m_Manager;
        private UFScanner m_Scanner;
        private UFMatcher m_Matcher;
        private bool m_IsInitialized = false;

        public bool IsConnected {
            get { return m_IsInitialized && m_Scanner != null; }
        }

        public string ScannerModel {
            get { return m_Scanner != null ? m_Scanner.ScannerType.ToString() : "N/A"; }
        }

        public string ScannerSerial {
            get { return m_Scanner != null ? m_Scanner.Serial : "N/A"; }
        }

        public bool Initialize() {
            try {
                m_Manager = new UFScannerManager(null);
                UFS_STATUS status = m_Manager.Init();
                if (status != UFS_STATUS.OK) return false;

                m_Matcher = new UFMatcher();

                if (m_Manager.Scanners.Count > 0) {
                    m_Scanner = m_Manager.Scanners[0];
                    m_IsInitialized = true;
                    return true;
                }
                return false;
            } catch {
                return false;
            }
        }

        public ScanResult Capture(int timeoutMs) {
            ScanResult result = new ScanResult();
            if (!IsConnected) {
                result.ErrorMessage = "Scanner não está conectado ou inicializado.";
                return result;
            }

            try {
                m_Scanner.Timeout = timeoutMs;
                UFS_STATUS status = m_Scanner.CaptureSingleImage();
                if (status != UFS_STATUS.OK) {
                    result.ErrorMessage = "Falha na captura: " + status;
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
                if (extStatus == UFS_STATUS.OK) {
                    result.Success = true;
                    result.Template = new byte[templateSize];
                    Array.Copy(template, result.Template, templateSize);
                    result.TemplateSize = templateSize;
                    result.Quality = quality;
                } else {
                    result.ErrorMessage = "Erro ao extrair minúcias: " + extStatus;
                }
            } catch (Exception ex) {
                result.ErrorMessage = "Exceção durante captura: " + ex.Message;
            }
            return result;
        }

        public ScanResult Capture() {
            return Capture(6000);
        }

        public bool Verify(byte[] templateA, int sizeA, byte[] templateB, int sizeB) {
            if (m_Matcher == null || templateA == null || templateB == null) return false;
            bool isMatch = false;
            UFM_STATUS status = m_Matcher.Verify(templateA, sizeA, templateB, sizeB, out isMatch);
            return status == UFM_STATUS.OK && isMatch;
        }

        public void Dispose() {
            try {
                if (m_Manager != null) {
                    m_Manager.Uninit();
                }
            } catch {}
            m_IsInitialized = false;
        }
    }
}
