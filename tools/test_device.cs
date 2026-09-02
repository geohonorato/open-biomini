using System;
using System.Runtime.InteropServices;

class Program {
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr hObject);

    static void Main() {
        string[] names = new string[] {
            @"\\.\SFRUSB-0",
            @"\\.\SFRUSB_0",
            @"\\.\SFRUSB",
            @"\\.\SFR300-0",
            @"\\.\Suprema0",
            @"\\.\BioMini0"
        };

        foreach (var name in names) {
            IntPtr h = CreateFile(name, 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
            int err = Marshal.GetLastWin32Error();
            Console.WriteLine(name + " -> Handle: " + h.ToInt64() + ", Win32 Error: " + err);
            if (h != IntPtr.Zero && h.ToInt64() != -1) {
                Console.WriteLine(">>> SUCESSO AO ABRIR: " + name);
                CloseHandle(h);
            }
        }
    }
}
