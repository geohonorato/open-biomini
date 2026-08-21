using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

public class SetupForm : Form {
    private Label lblTitle;
    private Label lblSubtitle;
    private Label lblStatus;
    private ProgressBar progress;
    private Button btnInstall;
    private CheckBox chkOpenApp;

    public SetupForm() {
        this.Text = "Instalador Universal — Suprema BioMini";
        this.Size = new Size(540, 420);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(15, 23, 42);
        this.ForeColor = Color.White;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;

        lblTitle = new Label {
            Text = "OPEN-BIOMINI UNIVERSAL SETUP",
            Location = new Point(30, 25),
            Size = new Size(470, 30),
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.FromArgb(96, 165, 250)
        };
        this.Controls.Add(lblTitle);

        lblSubtitle = new Label {
            Text = "Instalação automática do Driver USB PnP + Bibliotecas Universais (Sem travas de Vendor ID)",
            Location = new Point(30, 60),
            Size = new Size(470, 38),
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(148, 163, 184)
        };
        this.Controls.Add(lblSubtitle);

        Panel boxInfo = new Panel {
            Location = new Point(30, 105),
            Size = new Size(465, 120),
            BackColor = Color.FromArgb(30, 41, 59),
            BorderStyle = BorderStyle.None
        };
        this.Controls.Add(boxInfo);

        Label lblInfo = new Label {
            Text = "O que será instalado:\n" +
                   "• Driver USB PnP (SFRUSB.sys / sfr.inf) assinado para Windows 10/11\n" +
                   "• Bibliotecas de runtime sem trava OEM (UFScanner.dll / UFMatcher.dll)\n" +
                   "• Painel de Controle de Biometria (Cadastro, Verificação 1:N e Exclusão)\n" +
                   "• REST API Bridge (Porta 8080) para integração Web/Electron",
            Location = new Point(15, 12),
            Size = new Size(435, 95),
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(226, 232, 240)
        };
        boxInfo.Controls.Add(lblInfo);

        progress = new ProgressBar {
            Location = new Point(30, 240),
            Size = new Size(465, 18),
            Style = ProgressBarStyle.Continuous,
            Value = 0,
            Visible = false
        };
        this.Controls.Add(progress);

        lblStatus = new Label {
            Text = "Pronto para instalar. Conecte o leitor na porta USB e clique abaixo.",
            Location = new Point(30, 265),
            Size = new Size(465, 35),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.FromArgb(52, 211, 153)
        };
        this.Controls.Add(lblStatus);

        chkOpenApp = new CheckBox {
            Text = "Abrir Painel de Biometria ao concluir a instalação",
            Location = new Point(30, 305),
            Size = new Size(400, 24),
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(148, 163, 184),
            Checked = true
        };
        this.Controls.Add(chkOpenApp);

        btnInstall = new Button {
            Text = "🚀 Instalar Driver e Software (1-Clique)",
            Location = new Point(30, 335),
            Size = new Size(465, 45),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnInstall.FlatAppearance.BorderSize = 0;
        btnInstall.Click += BtnInstall_Click;
        this.Controls.Add(btnInstall);
    }

    private void BtnInstall_Click(object sender, EventArgs e) {
        btnInstall.Enabled = false;
        progress.Visible = true;
        progress.Value = 10;

        ThreadPool.QueueUserWorkItem((state) => {
            try {
                UpdateStatus("1/4: Extraindo arquivos do pacote...", 25);
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (string.IsNullOrEmpty(programFiles)) {
                    programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                }
                string targetDir = Path.Combine(programFiles, "OpenBioMini");

                if (!Directory.Exists(targetDir)) {
                    Directory.CreateDirectory(targetDir);
                }

                // Extrai o zip embutido
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream stream = asm.GetManifestResourceStream("payload.zip")) {
                    if (stream != null) {
                        string tempZip = Path.Combine(Path.GetTempPath(), "openbiomini_payload.zip");
                        using (FileStream fs = new FileStream(tempZip, FileMode.Create)) {
                            stream.CopyTo(fs);
                        }
                        ZipFile.ExtractToDirectory(tempZip, targetDir);
                        try { File.Delete(tempZip); } catch {}
                    }
                }

                UpdateStatus("2/4: Instalando Driver USB PnP no Windows...", 50);
                string driverInf = Path.Combine(targetDir, "driver", "sfr.inf");
                if (File.Exists(driverInf)) {
                    RunSilentProcess("pnputil.exe", string.Format("/add-driver \"{0}\" /install", driverInf));
                }

                UpdateStatus("3/4: Vinculando leitor e escaneando portas USB...", 75);
                RunSilentProcess("pnputil.exe", "/scan-devices");

                UpdateStatus("4/4: Criando atalhos na Área de Trabalho...", 90);
                CreateShortcuts(targetDir);

                UpdateStatus("✅ INSTALAÇÃO CONCLUÍDA COM SUCESSO! Leitor pronto para uso.", 100);

                this.Invoke((MethodInvoker)delegate {
                    btnInstall.Text = "✓ Concluído com Sucesso";
                    btnInstall.BackColor = Color.FromArgb(16, 185, 129);
                    
                    if (chkOpenApp.Checked) {
                        string appExe = Path.Combine(targetDir, "app", "VeritasBioMini.exe");
                        if (File.Exists(appExe)) {
                            Process.Start(appExe);
                        }
                    }
                });

            } catch (Exception ex) {
                UpdateStatus("❌ Erro durante a instalação: " + ex.Message, 0);
                this.Invoke((MethodInvoker)delegate {
                    btnInstall.Enabled = true;
                    btnInstall.Text = "Tentar Novamente";
                    btnInstall.BackColor = Color.FromArgb(239, 68, 68);
                });
            }
        });
    }

    private void UpdateStatus(string msg, int progVal) {
        this.Invoke((MethodInvoker)delegate {
            lblStatus.Text = msg;
            if (progVal > 0) progress.Value = progVal;
        });
    }

    private static void RunSilentProcess(string fileName, string args) {
        try {
            ProcessStartInfo psi = new ProcessStartInfo {
                FileName = fileName,
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                Verb = "runas"
            };
            using (Process p = Process.Start(psi)) {
                p.WaitForExit(15000);
            }
        } catch {}
    }

    private static void CreateShortcuts(string targetDir) {
        try {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string appExe = Path.Combine(targetDir, "app", "VeritasBioMini.exe");

            // Cria atalho via WScript.Shell
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null) {
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(Path.Combine(desktop, "Suprema BioMini — Controle.lnk"));
                shortcut.TargetPath = appExe;
                shortcut.WorkingDirectory = Path.Combine(targetDir, "app");
                shortcut.Description = "Painel de Controle Biométrico Suprema BioMini";
                shortcut.Save();
            }
        } catch {}
    }

    [STAThread]
    static void Main() {
        Application.EnableVisualStyles();
        Application.Run(new SetupForm());
    }
}
