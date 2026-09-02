using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;

public class Program {
    public static void Main(string[] args) {
        // args: <type: enroll|verify|test> <name> <id> <qualityOrScore> [printerName]
        string type = (args.Length > 0) ? args[0].ToLower() : "test";
        string name = (args.Length > 1) ? args[1] : "Geovanni Honorato";
        string id = (args.Length > 2) ? args[2] : DateTime.Now.Ticks.ToString().Substring(10);
        string score = (args.Length > 3) ? args[3] : "98";
        string printerName = (args.Length > 4) ? args[4] : "EPSON TM-T20X Receipt6";

        byte[] receiptBytes = BuildEscPosReceipt(type, name, id, score);
        bool ok = RawPrinterHelper.SendBytesToPrinter(printerName, receiptBytes);

        if (ok) {
            Console.WriteLine("{\"success\":true,\"message\":\"Cupom impresso com sucesso na " + printerName + "\"}");
        } else {
            Console.WriteLine("{\"success\":false,\"error\":\"Falha ao enviar cupom para a impressora " + printerName + "\"}");
        }
    }

    private static byte[] BuildEscPosReceipt(string type, string name, string id, string score) {
        List<byte> bytes = new List<byte>();

        // 1. Reset / Inicializar Impressora ESC @
        bytes.AddRange(new byte[] { 0x1B, 0x40 });

        // 2. Centralizado + Negrito + Título Grande (ESC ! 0x30)
        bytes.AddRange(new byte[] { 0x1B, 0x61, 0x01 }); // Center
        bytes.AddRange(new byte[] { 0x1B, 0x21, 0x30 }); // Double Height + Double Width

        if (type == "enroll") {
            bytes.AddRange(Encoding.ASCII.GetBytes("CADASTRO BIOMETRICO\r\n"));
        } else if (type == "verify") {
            bytes.AddRange(Encoding.ASCII.GetBytes("COMPROVANTE DE PONTO\r\n"));
        } else {
            bytes.AddRange(Encoding.ASCII.GetBytes("TESTE DE IMPRESSAO\r\n"));
        }

        // 3. Subtítulo normal
        bytes.AddRange(new byte[] { 0x1B, 0x21, 0x00 }); // Normal size
        bytes.AddRange(Encoding.ASCII.GetBytes("SISTEMA BIOMETRICO - VERITAS\r\n"));
        bytes.AddRange(Encoding.ASCII.GetBytes("==========================================\r\n"));

        // 4. Detalhes Alinhados à Esquerda
        bytes.AddRange(new byte[] { 0x1B, 0x61, 0x00 }); // Left align
        bytes.AddRange(Encoding.ASCII.GetBytes("Colaborador : " + name + "\r\n"));
        bytes.AddRange(Encoding.ASCII.GetBytes("ID Registro : #" + id + "\r\n"));
        bytes.AddRange(Encoding.ASCII.GetBytes("Data e Hora : " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + "\r\n"));
        bytes.AddRange(Encoding.ASCII.GetBytes("Dispositivo : Suprema BioMini (PID 0400)\r\n"));

        if (type == "enroll") {
            bytes.AddRange(Encoding.ASCII.GetBytes("Qualidade   : " + score + "% (Excelente)\r\n"));
            bytes.AddRange(Encoding.ASCII.GetBytes("Status      : Digital Gravada com Sucesso\r\n"));
        } else {
            bytes.AddRange(Encoding.ASCII.GetBytes("Similaridade: " + score + "%\r\n"));
            bytes.AddRange(Encoding.ASCII.GetBytes("Status      : Autenticacao Autorizada\r\n"));
        }

        bytes.AddRange(Encoding.ASCII.GetBytes("==========================================\r\n"));

        // 5. Rodapé Centralizado
        bytes.AddRange(new byte[] { 0x1B, 0x61, 0x01 }); // Center align
        bytes.AddRange(Encoding.ASCII.GetBytes("AUTENTICADO DIGITALMENTE\r\n"));
        bytes.AddRange(Encoding.ASCII.GetBytes("Desenvolvido por Geovanni Honorato\r\n\r\n\r\n\r\n"));

        // 6. Avanço e Corte Automático Total (GS V A 3)
        bytes.AddRange(new byte[] { 0x1D, 0x56, 0x41, 0x03 });

        return bytes.ToArray();
    }
}

public class RawPrinterHelper {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public class DOCINFOA {
        [MarshalAs(UnmanagedType.LPStr)] public string pDocName;
        [MarshalAs(UnmanagedType.LPStr)] public string pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string pDataType;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

    [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    public static bool SendBytesToPrinter(string szPrinterName, byte[] pBytes) {
        IntPtr pUnmanagedBytes = Marshal.AllocCoTaskMem(pBytes.Length);
        Marshal.Copy(pBytes, 0, pUnmanagedBytes, pBytes.Length);
        IntPtr hPrinter = IntPtr.Zero;
        DOCINFOA di = new DOCINFOA { pDocName = "Biometria Receipt", pDataType = "RAW" };
        bool success = false;
        if (OpenPrinter(szPrinterName.Normalize(), out hPrinter, IntPtr.Zero)) {
            if (StartDocPrinter(hPrinter, 1, di)) {
                if (StartPagePrinter(hPrinter)) {
                    int written;
                    success = WritePrinter(hPrinter, pUnmanagedBytes, pBytes.Length, out written);
                    EndPagePrinter(hPrinter);
                }
                EndDocPrinter(hPrinter);
            }
            ClosePrinter(hPrinter);
        }
        Marshal.FreeCoTaskMem(pUnmanagedBytes);
        return success;
    }
}
