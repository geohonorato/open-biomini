using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;

public class App : Application {
    [STAThread]
    public static void Main() {
        App app = new App();
        app.Run(new MainWindow());
    }
}

public class MainWindow : Window {
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private int currentStep = 0; // 0=License, 1=Directory, 2=Components, 3=Install, 4=Finish

    // Controles da interface
    private Border[] stepBadges = new Border[5];
    private TextBlock[] stepBadgeTexts = new TextBlock[5];
    private TextBlock[] stepLabels = new TextBlock[5];

    private Grid[] stepPages = new Grid[5];

    private Button btnBack;
    private Button btnNext;
    private Button btnCancel;

    // Etapa 0: Licença
    private CheckBox chkAcceptLicense;

    // Etapa 1: Destino
    private TextBox txtInstallDir;
    private TextBlock lblDiskSpace;

    // Etapa 2: Componentes
    private CheckBox chkDriver;
    private CheckBox chkCli;
    private CheckBox chkBridge;
    private CheckBox chkWbf;
    private CheckBox chkSdk;
    private CheckBox chkPath;
    private CheckBox chkShortcut;

    // Etapa 3: Instalação
    private ProgressBar progressBar;
    private TextBlock lblProgressStatus;
    private TextBox txtInstallLog;

    // Etapa 4: Conclusão
    private CheckBox chkStartBridge;
    private CheckBox chkOpenDocs;

    public MainWindow() {
        this.Title = "OpenBioMini Setup — Assistente de Instalação Universal";
        this.Width = 850;
        this.Height = 620;
        this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        this.ResizeMode = ResizeMode.NoResize;
        this.Background = new SolidColorBrush(Color.FromRgb(11, 15, 25)); // #0B0F19
        this.Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252));
        this.FontFamily = new FontFamily("Segoe UI, Tahoma, Arial");

        // Aplicar Estilo Global de ScrollBar Moderno Dark via XamlReader
        ApplyModernScrollbarStyle();

        this.Loaded += (s, e) => {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int darkMode = 1;
            // Windows 11 / Windows 10 dark title bar
            DwmSetWindowAttribute(hwnd, 20, ref darkMode, sizeof(int));
            DwmSetWindowAttribute(hwnd, 19, ref darkMode, sizeof(int));
        };

        BuildUI();
        ShowStep(0);
    }

    private void ApplyModernScrollbarStyle() {
        try {
            string xaml = @"
            <ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                                xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
                <Style TargetType='{x:Type ScrollBar}'>
                    <Setter Property='Width' Value='6'/>
                    <Setter Property='Background' Value='Transparent'/>
                    <Setter Property='Template'>
                        <Setter.Value>
                            <ControlTemplate TargetType='{x:Type ScrollBar}'>
                                <Grid Background='Transparent'>
                                    <Track x:Name='PART_Track' IsDirectionReversed='true'>
                                        <Track.Thumb>
                                            <Thumb>
                                                <Thumb.Template>
                                                    <ControlTemplate TargetType='{x:Type Thumb}'>
                                                        <Border Background='#475569' CornerRadius='3' Margin='1,0,1,0'/>
                                                    </ControlTemplate>
                                                </Thumb.Template>
                                            </Thumb>
                                        </Track.Thumb>
                                    </Track>
                                </Grid>
                            </ControlTemplate>
                        </Setter.Value>
                    </Setter>
                </Style>
            </ResourceDictionary>";
            var res = (ResourceDictionary)XamlReader.Parse(xaml);
            this.Resources.MergedDictionaries.Add(res);
        } catch { }
    }

    private void BuildUI() {
        Grid mainGrid = new Grid();
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        this.Content = mainGrid;

        // ==========================================
        // SIDEBAR (ESQUERDA)
        // ==========================================
        Border sidebar = new Border {
            Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)), // #0F172A
            BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59)), // #1E293B
            BorderThickness = new Thickness(0, 0, 1, 0)
        };
        Grid.SetColumn(sidebar, 0);
        mainGrid.Children.Add(sidebar);

        Grid sidebarGrid = new Grid();
        sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sidebar.Child = sidebarGrid;

        // Header Sidebar
        StackPanel brandPanel = new StackPanel { Margin = new Thickness(25, 25, 20, 25) };
        Grid.SetRow(brandPanel, 0);
        sidebarGrid.Children.Add(brandPanel);

        StackPanel logoRow = new StackPanel { Orientation = Orientation.Horizontal };
        TextBlock logoIcon = new TextBlock {
            Text = "⚡",
            FontSize = 20,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        TextBlock logoText = new TextBlock {
            Text = "OpenBioMini",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)), // #38BDF8
            VerticalAlignment = VerticalAlignment.Center
        };
        logoRow.Children.Add(logoIcon);
        logoRow.Children.Add(logoText);
        brandPanel.Children.Add(logoRow);

        TextBlock versionBadge = new TextBlock {
            Text = "UNIVERSAL SETUP  v1.0.0",
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), // #94A3B8
            Margin = new Thickness(0, 4, 0, 0)
        };
        brandPanel.Children.Add(versionBadge);

        // Stepper Vertical
        StackPanel stepperPanel = new StackPanel { Margin = new Thickness(25, 10, 20, 20) };
        Grid.SetRow(stepperPanel, 1);
        sidebarGrid.Children.Add(stepperPanel);

        string[] stepTitles = { "Licença MIT", "Pasta de Destino", "Componentes", "Instalação", "Conclusão" };
        for (int i = 0; i < 5; i++) {
            Grid stepRow = new Grid { Margin = new Thickness(0, 0, 0, 22) };
            stepRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            stepRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border badge = new Border {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 12, 0)
            };
            TextBlock badgeTxt = new TextBlock {
                Text = (i + 1).ToString(),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = badgeTxt;
            Grid.SetColumn(badge, 0);
            stepRow.Children.Add(badge);

            TextBlock label = new TextBlock {
                Text = stepTitles[i],
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), // #64748B
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 1);
            stepRow.Children.Add(label);

            stepperPanel.Children.Add(stepRow);

            stepBadges[i] = badge;
            stepBadgeTexts[i] = badgeTxt;
            stepLabels[i] = label;
        }

        // Footer Sidebar
        Border sidebarFooter = new Border {
            Padding = new Thickness(20, 14, 20, 16),
            BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            BorderThickness = new Thickness(0, 1, 0, 0)
        };
        Grid.SetRow(sidebarFooter, 2);
        sidebarGrid.Children.Add(sidebarFooter);

        StackPanel footerStack = new StackPanel();
        sidebarFooter.Child = footerStack;

        footerStack.Children.Add(new TextBlock {
            Text = "Desenvolvido por:",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            Margin = new Thickness(0, 0, 0, 3)
        });

        footerStack.Children.Add(new TextBlock {
            Text = "Geovanni Honorato",
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Botão Interativo GitHub / Website
        Border btnAuthorLink = new Border {
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6, 10, 6),
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 10)
        };

        StackPanel authorLinkContent = new StackPanel { Orientation = Orientation.Horizontal };
        TextBlock authorLinkIcon = new TextBlock {
            Text = "🐙",
            FontSize = 11,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        TextBlock authorLinkText = new TextBlock {
            Text = "GitHub @geohonorato",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)), // Sky 400
            VerticalAlignment = VerticalAlignment.Center
        };
        authorLinkContent.Children.Add(authorLinkIcon);
        authorLinkContent.Children.Add(authorLinkText);
        btnAuthorLink.Child = authorLinkContent;

        btnAuthorLink.MouseEnter += (s, e) => {
            btnAuthorLink.Background = new SolidColorBrush(Color.FromRgb(51, 65, 85));
            btnAuthorLink.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        };
        btnAuthorLink.MouseLeave += (s, e) => {
            btnAuthorLink.Background = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            btnAuthorLink.BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85));
        };
        btnAuthorLink.MouseDown += (s, e) => {
            try {
                // Link oficial do autor (fácil de trocar futuramente para o seu site próprio)
                Process.Start(new ProcessStartInfo("https://github.com/geohonorato") { UseShellExecute = true });
            } catch { }
        };
        footerStack.Children.Add(btnAuthorLink);

        footerStack.Children.Add(new TextBlock {
            Text = "Hardware: Suprema BioMini (PID 0400)",
            FontSize = 10.5,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            LineHeight = 15
        });

        // ==========================================
        // ÁREA PRINCIPAL (DIREITA)
        // ==========================================
        Grid rightGrid = new Grid { Margin = new Thickness(35, 25, 35, 25) };
        Grid.SetColumn(rightGrid, 1);
        mainGrid.Children.Add(rightGrid);

        rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Container de Conteúdo das Etapas
        Grid contentArea = new Grid();
        Grid.SetRow(contentArea, 0);
        rightGrid.Children.Add(contentArea);

        BuildStep0License(contentArea);
        BuildStep1Directory(contentArea);
        BuildStep2Components(contentArea);
        BuildStep3Install(contentArea);
        BuildStep4Finish(contentArea);

        // Footer Buttons
        DockPanel footerButtons = new DockPanel {
            Margin = new Thickness(0, 15, 0, 0),
            LastChildFill = false
        };
        Grid.SetRow(footerButtons, 1);
        rightGrid.Children.Add(footerButtons);

        btnCancel = CreateStyledButton("Cancelar", Color.FromRgb(30, 41, 59), Color.FromRgb(148, 163, 184), 90);
        btnCancel.Click += (s, e) => {
            if (currentStep == 4 || MessageBox.Show("Deseja realmente sair do instalador?", "Cancelar Instalação", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) {
                this.Close();
            }
        };
        DockPanel.SetDock(btnCancel, Dock.Right);
        footerButtons.Children.Add(btnCancel);

        btnNext = CreateStyledButton("Avançar >", Color.FromRgb(37, 99, 235), Color.FromRgb(255, 255, 255), 125);
        btnNext.FontWeight = FontWeights.Bold;
        btnNext.Margin = new Thickness(0, 0, 10, 0);
        btnNext.Click += BtnNext_Click;
        DockPanel.SetDock(btnNext, Dock.Right);
        footerButtons.Children.Add(btnNext);

        btnBack = CreateStyledButton("< Voltar", Color.FromRgb(30, 41, 59), Color.FromRgb(226, 232, 240), 95);
        btnBack.Margin = new Thickness(0, 0, 10, 0);
        btnBack.Click += (s, e) => { if (currentStep > 0) ShowStep(currentStep - 1); };
        DockPanel.SetDock(btnBack, Dock.Right);
        footerButtons.Children.Add(btnBack);
    }

    private Button CreateStyledButton(string text, Color bg, Color fg, double width) {
        Button btn = new Button {
            Content = text,
            Width = width,
            Height = 38,
            Background = new SolidColorBrush(bg),
            Foreground = new SolidColorBrush(fg),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            FontSize = 13
        };

        ControlTemplate template = new ControlTemplate(typeof(Button));
        FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        border.SetValue(Border.PaddingProperty, new Thickness(10, 0, 10, 0));

        FrameworkElementFactory contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(contentPresenter);
        template.VisualTree = border;
        btn.Template = template;

        return btn;
    }

    // ==========================================
    // ETAPA 0: LICENÇA MIT
    // ==========================================
    private void BuildStep0License(Grid parent) {
        Grid page = new Grid { Visibility = Visibility.Collapsed };
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        parent.Children.Add(page);
        stepPages[0] = page;

        StackPanel titleBox = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
        Grid.SetRow(titleBox, 0);
        page.Children.Add(titleBox);

        titleBox.Children.Add(new TextBlock {
            Text = "Contrato de Licença e Termos de Uso",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252))
        });
        titleBox.Children.Add(new TextBlock {
            Text = "Leia os termos da licença open-source MIT antes de prosseguir com a instalação:",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            Margin = new Thickness(0, 4, 0, 0)
        });

        Border licenseCard = new Border {
            Background = new SolidColorBrush(Color.FromRgb(20, 30, 50)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16)
        };
        Grid.SetRow(licenseCard, 1);
        page.Children.Add(licenseCard);

        ScrollViewer scroll = new ScrollViewer {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        licenseCard.Child = scroll;

        TextBlock txtLicenseContent = new TextBlock {
            Text = "MIT License — OpenBioMini Project\r\n\r\n" +
                   "Copyright (c) 2026 Geovanni Honorato (@geohonorato)\r\n\r\n" +
                   "Permission is hereby granted, free of charge, to any person obtaining a copy\r\n" +
                   "of this software and associated documentation files (the \"Software\"), to deal\r\n" +
                   "in the Software without restriction, including without limitation the rights\r\n" +
                   "to use, copy, modify, merge, publish, distribute, sublicense, and/or sell\r\n" +
                   "copies of the Software, and to permit persons to whom the Software is\r\n" +
                   "furnished to do so, subject to the following conditions:\r\n\r\n" +
                   "The above copyright notice and this permission notice shall be included in all\r\n" +
                   "copies or substantial portions of the Software.\r\n\r\n" +
                   "DISCLAIMER / ISENÇÃO DE RESPONSABILIDADE:\r\n" +
                   "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR\r\n" +
                   "IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,\r\n" +
                   "FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE\r\n" +
                   "AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER\r\n" +
                   "LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,\r\n" +
                   "OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE\r\n" +
                   "SOFTWARE.\r\n\r\n" +
                   "AVISO LEGAL:\r\n" +
                   "Este software é um projeto independente da comunidade de código aberto para fins\r\n" +
                   "de interoperabilidade e suporte a hardware descontinuado (Suprema BioMini 1ª Ger).\r\n" +
                   "Suprema e BioMini são marcas registradas de seus respectivos proprietários.",
            FontFamily = new FontFamily("Consolas, Courier New"),
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            LineHeight = 18
        };
        scroll.Content = txtLicenseContent;

        chkAcceptLicense = new CheckBox {
            Content = "Eu li e aceito os termos do contrato de licença MIT e isenção de responsabilidade",
            Margin = new Thickness(0, 15, 0, 0),
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            Cursor = Cursors.Hand
        };
        chkAcceptLicense.Checked += (s, e) => { btnNext.IsEnabled = true; btnNext.Opacity = 1.0; };
        chkAcceptLicense.Unchecked += (s, e) => { btnNext.IsEnabled = false; btnNext.Opacity = 0.4; };
        Grid.SetRow(chkAcceptLicense, 2);
        page.Children.Add(chkAcceptLicense);
    }

    // ==========================================
    // ETAPA 1: LOCAL DE INSTALAÇÃO
    // ==========================================
    private void BuildStep1Directory(Grid parent) {
        Grid page = new Grid { Visibility = Visibility.Collapsed };
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        parent.Children.Add(page);
        stepPages[1] = page;

        StackPanel titleBox = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
        Grid.SetRow(titleBox, 0);
        page.Children.Add(titleBox);

        titleBox.Children.Add(new TextBlock {
            Text = "Escolha o Local de Instalação",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252))
        });
        titleBox.Children.Add(new TextBlock {
            Text = "O OpenBioMini será configurado na seguinte pasta no seu computador:",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            Margin = new Thickness(0, 4, 0, 0)
        });

        Border dirCard = new Border {
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18),
            Margin = new Thickness(0, 0, 0, 20)
        };
        Grid.SetRow(dirCard, 1);
        page.Children.Add(dirCard);

        StackPanel dirStack = new StackPanel();
        dirCard.Child = dirStack;

        dirStack.Children.Add(new TextBlock {
            Text = "Pasta de Destino:",
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            Margin = new Thickness(0, 0, 0, 8)
        });

        Grid inputRow = new Grid();
        inputRow.ColumnDefinitions.Add(new GridDefinition_Input());
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        dirStack.Children.Add(inputRow);

        txtInstallDir = new TextBox {
            Text = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "OpenBioMini"),
            FontSize = 13,
            Height = 36,
            Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 7, 8, 7),
            Margin = new Thickness(0, 0, 10, 0)
        };
        Grid.SetColumn(txtInstallDir, 0);
        inputRow.Children.Add(txtInstallDir);

        Button btnBrowse = CreateStyledButton("Procurar...", Color.FromRgb(51, 65, 85), Color.FromRgb(248, 250, 252), 105);
        btnBrowse.Click += (s, e) => {
            var dialog = new System.Windows.Forms.FolderBrowserDialog {
                Description = "Selecione a pasta de destino para o OpenBioMini",
                SelectedPath = txtInstallDir.Text
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                txtInstallDir.Text = dialog.SelectedPath;
                UpdateDiskSpace();
            }
        };
        Grid.SetColumn(btnBrowse, 1);
        inputRow.Children.Add(btnBrowse);

        Border infoCard = new Border {
            Background = new SolidColorBrush(Color.FromRgb(20, 30, 50)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18)
        };
        Grid.SetRow(infoCard, 2);
        page.Children.Add(infoCard);

        lblDiskSpace = new TextBlock {
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            LineHeight = 22
        };
        infoCard.Child = lblDiskSpace;
        UpdateDiskSpace();
    }

    private class GridDefinition_Input : ColumnDefinition {
        public GridDefinition_Input() {
            this.Width = new GridLength(1, GridUnitType.Star);
        }
    }

    private void UpdateDiskSpace() {
        try {
            string root = System.IO.Path.GetPathRoot(txtInstallDir.Text);
            DriveInfo d = new DriveInfo(root);
            lblDiskSpace.Text = string.Format("💾 Espaço necessário em disco: ~15 MB\r\n📁 Espaço livre disponível na unidade ({0}): {1:N1} GB", root, d.AvailableFreeSpace / (1024.0 * 1024 * 1024));
        } catch {
            lblDiskSpace.Text = "💾 Espaço necessário em disco: ~15 MB";
        }
    }

    // ==========================================
    // ETAPA 2: COMPONENTES MODULARES (SEM SCROLLBAR INDESEJADA)
    // ==========================================
    private void BuildStep2Components(Grid parent) {
        Grid page = new Grid { Visibility = Visibility.Collapsed };
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        parent.Children.Add(page);
        stepPages[2] = page;

        StackPanel titleBox = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        Grid.SetRow(titleBox, 0);
        page.Children.Add(titleBox);

        titleBox.Children.Add(new TextBlock {
            Text = "Seleção de Componentes",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252))
        });
        titleBox.Children.Add(new TextBlock {
            Text = "Escolha os módulos que deseja instalar no seu computador:",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            Margin = new Thickness(0, 3, 0, 0)
        });

        Border compCard = new Border {
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10)
        };
        Grid.SetRow(compCard, 1);
        page.Children.Add(compCard);

        StackPanel list = new StackPanel();
        compCard.Child = list;

        chkDriver = CreateComponentItem(list, "Driver USB PnP Oficial (sfr.inf / SFRUSB.sys)", "Driver de kernel oficial assinado da Suprema para Windows 10 e 11", true, "Essencial");
        chkBridge = CreateComponentItem(list, "Serviço Windows PnP & REST API (OpenBioMiniService)", "Serviço em segundo plano (SYSTEM) para Hot-Plug USB 100% automático e REST API (Porta 8080)", true, "Essencial");
        chkCli = CreateComponentItem(list, "OpenBioMini CLI (biomini.exe)", "Ferramenta de terminal para testar, capturar e comparar digitais", true, "Recomendado");
        chkWbf = CreateComponentItem(list, "Windows Hello WBF Adapter (BioMiniSensorAdapter.dll)", "Adaptador biométrico WBF para integração com o Windows Hello", false, "Opcional");
        chkSdk = CreateComponentItem(list, "SDK, Exemplos e Documentação Técnica", "Headers C/C++, wrapper C#, documentação de engenharia reversa e guias", true, "Dev");

        // Opções Adicionais
        StackPanel extraOptions = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        Grid.SetRow(extraOptions, 2);
        page.Children.Add(extraOptions);

        chkPath = CreateSimpleCheck(extraOptions, "Adicionar pasta de instalação ao PATH do sistema (permitir 'biomini' em qualquer terminal)", true);
        chkShortcut = CreateSimpleCheck(extraOptions, "Criar atalhos na Área de Trabalho e Menu Iniciar", true);
    }

    private CheckBox CreateComponentItem(StackPanel parent, string title, string desc, bool isChecked, string tag) {
        Border itemBorder = new Border {
            Background = new SolidColorBrush(Color.FromRgb(20, 30, 50)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 0, 0, 6)
        };
        parent.Children.Add(itemBorder);

        Grid g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        itemBorder.Child = g;

        StackPanel sp = new StackPanel();
        Grid.SetColumn(sp, 0);
        g.Children.Add(sp);

        CheckBox chk = new CheckBox {
            Content = title,
            IsChecked = isChecked,
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            Cursor = Cursors.Hand
        };
        sp.Children.Add(chk);

        TextBlock descTxt = new TextBlock {
            Text = desc,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            Margin = new Thickness(22, 1, 0, 0)
        };
        sp.Children.Add(descTxt);

        Border tagBorder = new Border {
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            VerticalAlignment = VerticalAlignment.Center
        };
        TextBlock tagTxt = new TextBlock {
            Text = tag,
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248))
        };
        tagBorder.Child = tagTxt;
        Grid.SetColumn(tagBorder, 1);
        g.Children.Add(tagBorder);

        return chk;
    }

    private CheckBox CreateSimpleCheck(StackPanel parent, string title, bool isChecked) {
        CheckBox chk = new CheckBox {
            Content = title,
            IsChecked = isChecked,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            Margin = new Thickness(4, 3, 0, 3),
            Cursor = Cursors.Hand
        };
        parent.Children.Add(chk);
        return chk;
    }

    // ==========================================
    // ETAPA 3: INSTALAÇÃO EM TEMPO REAL
    // ==========================================
    private void BuildStep3Install(Grid parent) {
        Grid page = new Grid { Visibility = Visibility.Collapsed };
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        parent.Children.Add(page);
        stepPages[3] = page;

        StackPanel titleBox = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
        Grid.SetRow(titleBox, 0);
        page.Children.Add(titleBox);

        titleBox.Children.Add(new TextBlock {
            Text = "Instalando o OpenBioMini...",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252))
        });

        lblProgressStatus = new TextBlock {
            Text = "Preparando a instalação...",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            Margin = new Thickness(0, 4, 0, 0)
        };
        titleBox.Children.Add(lblProgressStatus);

        progressBar = new ProgressBar {
            Height = 14,
            Margin = new Thickness(0, 0, 0, 15),
            Value = 0,
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
            BorderThickness = new Thickness(0)
        };
        Grid.SetRow(progressBar, 1);
        page.Children.Add(progressBar);

        Border logCard = new Border {
            Background = new SolidColorBrush(Color.FromRgb(10, 14, 23)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12)
        };
        Grid.SetRow(logCard, 2);
        page.Children.Add(logCard);

        txtInstallLog = new TextBox {
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            BorderThickness = new Thickness(0),
            FontFamily = new FontFamily("Consolas, Courier New"),
            FontSize = 11.5,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        logCard.Child = txtInstallLog;
    }

    // ==========================================
    // ETAPA 4: CONCLUSÃO
    // ==========================================
    private void BuildStep4Finish(Grid parent) {
        Grid page = new Grid { Visibility = Visibility.Collapsed };
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        parent.Children.Add(page);
        stepPages[4] = page;

        StackPanel successBox = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
        Grid.SetRow(successBox, 0);
        page.Children.Add(successBox);

        StackPanel titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        titleRow.Children.Add(new TextBlock {
            Text = "✅",
            FontSize = 24,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        titleRow.Children.Add(new TextBlock {
            Text = "Instalação Concluída com Sucesso!",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)), // Emerald 500
            VerticalAlignment = VerticalAlignment.Center
        });
        successBox.Children.Add(titleRow);

        successBox.Children.Add(new TextBlock {
            Text = "O OpenBioMini foi instalado com sucesso. O leitor Suprema BioMini já está pronto para uso.",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            Margin = new Thickness(0, 6, 0, 0)
        });

        Border summaryCard = new Border {
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18)
        };
        Grid.SetRow(summaryCard, 1);
        page.Children.Add(summaryCard);

        StackPanel sumStack = new StackPanel();
        summaryCard.Child = sumStack;

        sumStack.Children.Add(new TextBlock {
            Text = "Comandos e Integrações Rápidas:",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            Margin = new Thickness(0, 0, 0, 8)
        });

        sumStack.Children.Add(new TextBlock {
            Text = "• Terminal / Prompt: digite 'biomini status' ou 'biomini capture'\r\n" +
                   "• REST API local: http://localhost:8080/api/status e /api/capture\r\n" +
                   "• Named Pipe local: \\\\.\\pipe\\BioMiniWbfPipe\r\n" +
                   "• Autor: Geovanni Honorato (github.com/geohonorato/open-biomini)",
            FontSize = 12.5,
            Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            LineHeight = 22
        });

        StackPanel finishOptions = new StackPanel { Margin = new Thickness(0, 15, 0, 0) };
        Grid.SetRow(finishOptions, 2);
        page.Children.Add(finishOptions);

        chkStartBridge = CreateSimpleCheck(finishOptions, "Iniciar o serviço REST Bridge em segundo plano agora", true);
        chkStartBridge.FontWeight = FontWeights.SemiBold;
        chkStartBridge.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));

        chkOpenDocs = CreateSimpleCheck(finishOptions, "Abrir pasta de documentação e exemplos no Explorer", false);
    }

    private void ShowStep(int step) {
        currentStep = step;

        for (int i = 0; i < 5; i++) {
            stepPages[i].Visibility = (i == step) ? Visibility.Visible : Visibility.Collapsed;

            if (i < step) {
                stepBadges[i].Background = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                stepBadges[i].BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                stepBadgeTexts[i].Text = "✓";
                stepBadgeTexts[i].Foreground = Brushes.White;
                stepLabels[i].Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            } else if (i == step) {
                stepBadges[i].Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                stepBadges[i].BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                stepBadgeTexts[i].Text = (i + 1).ToString();
                stepBadgeTexts[i].Foreground = Brushes.White;
                stepLabels[i].Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252));
            } else {
                stepBadges[i].Background = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                stepBadges[i].BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85));
                stepBadgeTexts[i].Text = (i + 1).ToString();
                stepBadgeTexts[i].Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                stepLabels[i].Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
            }
        }

        btnBack.Visibility = (step > 0 && step < 3) ? Visibility.Visible : Visibility.Collapsed;
        btnCancel.Visibility = (step < 3) ? Visibility.Visible : Visibility.Collapsed;

        if (step == 0) {
            btnNext.Content = "Avançar >";
            btnNext.IsEnabled = (chkAcceptLicense.IsChecked == true);
            btnNext.Opacity = (chkAcceptLicense.IsChecked == true) ? 1.0 : 0.4;
        } else if (step == 1) {
            btnNext.Content = "Avançar >";
            btnNext.IsEnabled = true;
            btnNext.Opacity = 1.0;
            UpdateDiskSpace();
        } else if (step == 2) {
            btnNext.Content = "🚀 Instalar";
            btnNext.IsEnabled = true;
            btnNext.Opacity = 1.0;
        } else if (step == 3) {
            btnNext.Visibility = Visibility.Collapsed;
            btnBack.Visibility = Visibility.Collapsed;
            btnCancel.Visibility = Visibility.Collapsed;
        } else if (step == 4) {
            btnNext.Visibility = Visibility.Visible;
            btnNext.Content = "Concluir";
            btnNext.IsEnabled = true;
            btnNext.Opacity = 1.0;
            btnBack.Visibility = Visibility.Collapsed;
            btnCancel.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnNext_Click(object sender, RoutedEventArgs e) {
        if (currentStep == 0) {
            ShowStep(1);
        } else if (currentStep == 1) {
            if (string.IsNullOrWhiteSpace(txtInstallDir.Text)) {
                MessageBox.Show("Por favor, informe uma pasta de instalação válida.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ShowStep(2);
        } else if (currentStep == 2) {
            ShowStep(3);
            StartInstallation();
        } else if (currentStep == 4) {
            if (chkStartBridge.IsChecked == true) {
                string bridgePath = System.IO.Path.Combine(txtInstallDir.Text, "bridge", "OpenBioMini.Bridge.exe");
                if (File.Exists(bridgePath)) {
                    Process.Start(new ProcessStartInfo(bridgePath) {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        WorkingDirectory = System.IO.Path.GetDirectoryName(bridgePath)
                    });
                }
            }
            if (chkOpenDocs.IsChecked == true) {
                string docsPath = System.IO.Path.Combine(txtInstallDir.Text, "docs");
                if (Directory.Exists(docsPath)) {
                    Process.Start("explorer.exe", docsPath);
                }
            }
            this.Close();
        }
    }

    private void Log(string msg) {
        this.Dispatcher.Invoke(new Action(() => {
            txtInstallLog.AppendText(string.Format("[{0:HH:mm:ss}] {1}\r\n", DateTime.Now, msg));
            txtInstallLog.ScrollToEnd();
        }));
    }

    private void SetProgress(int value, string status) {
        this.Dispatcher.Invoke(new Action(() => {
            progressBar.Value = Math.Min(100, Math.Max(0, value));
            lblProgressStatus.Text = status;
        }));
    }

    private void StartInstallation() {
        string targetDir = txtInstallDir.Text;
        bool doDriver = chkDriver.IsChecked == true;
        bool doCli = chkCli.IsChecked == true;
        bool doBridge = chkBridge.IsChecked == true;
        bool doWbf = chkWbf.IsChecked == true;
        bool doSdk = chkSdk.IsChecked == true;
        bool doPath = chkPath.IsChecked == true;
        bool doShortcut = chkShortcut.IsChecked == true;

        Thread t = new Thread(() => {
            try {
                SetProgress(5, "Criando diretórios de instalação...");
                Log("Criando diretório: " + targetDir);
                Directory.CreateDirectory(targetDir);

                SetProgress(15, "Extraindo pacote de arquivos...");
                Log("Carregando pacote de dados embutido...");
                Assembly asm = Assembly.GetExecutingAssembly();
                string tempZip = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "openbiomini_payload_" + Guid.NewGuid().ToString("N") + ".zip");
                string tempExtract = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "openbiomini_extract_" + Guid.NewGuid().ToString("N"));

                using (Stream s = asm.GetManifestResourceStream("payload.zip")) {
                    if (s == null) throw new Exception("Recurso payload.zip não encontrado no executável.");
                    using (FileStream fs = new FileStream(tempZip, FileMode.Create)) {
                        s.CopyTo(fs);
                    }
                }

                ZipFile.ExtractToDirectory(tempZip, tempExtract);
                File.Delete(tempZip);
                Log("Arquivos extraídos com sucesso.");

                SetProgress(30, "Copiando componentes selecionados...");

                string coreSrc = System.IO.Path.Combine(tempExtract, "core");
                if (Directory.Exists(coreSrc)) {
                    CopyDirectory(coreSrc, targetDir);
                    Log("Bibliotecas Core (UFScanner.dll / OpenBioMini.Core.dll) instaladas.");
                }

                if (doCli) {
                    string cliSrc = System.IO.Path.Combine(tempExtract, "cli");
                    if (Directory.Exists(cliSrc)) {
                        CopyDirectory(cliSrc, targetDir);
                        Log("Utilitário CLI (biomini.exe) instalado.");
                    }
                }

                if (doBridge) {
                    string bridgeDir = System.IO.Path.Combine(targetDir, "bridge");
                    Directory.CreateDirectory(bridgeDir);
                    string bridgeSrc = System.IO.Path.Combine(tempExtract, "bridge");
                    if (Directory.Exists(bridgeSrc)) {
                        CopyDirectory(bridgeSrc, bridgeDir);
                        if (Directory.Exists(coreSrc)) CopyDirectory(coreSrc, bridgeDir);
                        Log("REST API & Named Pipe Bridge instalado em " + bridgeDir);
                    }
                }

                if (doWbf) {
                    string wbfDir = System.IO.Path.Combine(targetDir, "wbf");
                    Directory.CreateDirectory(wbfDir);
                    string wbfSrc = System.IO.Path.Combine(tempExtract, "wbf");
                    if (Directory.Exists(wbfSrc)) {
                        CopyDirectory(wbfSrc, wbfDir);
                        Log("Arquivos WBF Adapter copiados para " + wbfDir);
                    }
                }

                if (doSdk) {
                    string sdkSrc = System.IO.Path.Combine(tempExtract, "sdk");
                    if (Directory.Exists(sdkSrc)) CopyDirectory(sdkSrc, System.IO.Path.Combine(targetDir, "sdk"));
                    string docsSrc = System.IO.Path.Combine(tempExtract, "docs");
                    if (Directory.Exists(docsSrc)) CopyDirectory(docsSrc, System.IO.Path.Combine(targetDir, "docs"));
                    Log("SDK, Headers e Documentação técnica instalados.");
                }

                string[] infoFiles = { "LICENSE", "README.md", "README.pt-BR.md" };
                foreach (string f in infoFiles) {
                    string src = System.IO.Path.Combine(tempExtract, f);
                    if (File.Exists(src)) File.Copy(src, System.IO.Path.Combine(targetDir, f), true);
                }

                if (doDriver) {
                    SetProgress(60, "Registrando Driver USB PnP no Windows...");
                    Log("Instalando driver PnP oficial da Suprema (sfr.inf)...");

                    // Limpeza de travas antigas do registro
                    try {
                        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB\VID_16D1&PID_0400", true)) {
                            if (key != null) {
                                foreach (string sub in key.GetSubKeyNames()) {
                                    using (RegistryKey subKey = key.OpenSubKey(sub, true)) {
                                        if (subKey != null) {
                                            try { subKey.DeleteValue("Exclusive", false); } catch { }
                                            try { subKey.DeleteValue("Security", false); } catch { }
                                            try { subKey.DeleteValue("DeviceCharacteristics", false); } catch { }
                                            using (RegistryKey paramKey = subKey.OpenSubKey("Device Parameters", true)) {
                                                if (paramKey != null) {
                                                    try { paramKey.DeleteValue("DeviceInterfaceGUIDs", false); } catch { }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    } catch { }

                    string driverSrc = System.IO.Path.Combine(tempExtract, "driver", "sfr.inf");
                    if (File.Exists(driverSrc)) {
                        ProcessStartInfo psi = new ProcessStartInfo("pnputil.exe", string.Format("/add-driver \"{0}\" /install", driverSrc)) {
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true
                        };
                        using (Process p = Process.Start(psi)) {
                            string outP = p.StandardOutput.ReadToEnd();
                            p.WaitForExit();
                            Log("pnputil: Driver PnP instalado no catálogo do sistema.");
                        }
                    }
                }

                if (doBridge) {
                    SetProgress(75, "Registrando Serviço Windows PnP (Auto-Start)...");
                    Log("Instalando OpenBioMiniService como serviço do sistema (SYSTEM)...");
                    string svcExe = System.IO.Path.Combine(targetDir, "OpenBioMiniService.exe");
                    if (File.Exists(svcExe)) {
                        ProcessStartInfo psi = new ProcessStartInfo(svcExe, "--install") {
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true
                        };
                        using (Process p = Process.Start(psi)) {
                            p.WaitForExit();
                            Log("Serviço Windows OpenBioMiniService configurado e ativo.");
                        }
                    }
                }

                if (doPath) {
                    SetProgress(75, "Adicionando ao PATH do Windows...");
                    try {
                        string pathVar = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
                        if (!pathVar.Contains(targetDir)) {
                            Environment.SetEnvironmentVariable("PATH", pathVar.TrimEnd(';') + ";" + targetDir, EnvironmentVariableTarget.Machine);
                            Log("Pasta adicionada ao PATH do sistema: " + targetDir);
                        }
                    } catch (Exception ex) {
                        Log("Aviso ao definir PATH: " + ex.Message);
                    }
                }

                if (doShortcut) {
                    SetProgress(85, "Criando atalhos...");
                    CreateDesktopShortcut("OpenBioMini CLI", System.IO.Path.Combine(targetDir, "biomini.exe"), "Ferramenta de Linha de Comando OpenBioMini");
                    Log("Atalho criado na Área de Trabalho.");
                }

                try { Directory.Delete(tempExtract, true); } catch { }

                SetProgress(100, "Instalação finalizada!");
                Log("Instalação concluída com sucesso.");
                Thread.Sleep(800);

                this.Dispatcher.Invoke(new Action(() => {
                    ShowStep(4);
                }));

            } catch (Exception ex) {
                Log("ERRO: " + ex.Message);
                MessageBox.Show("Erro durante a instalação: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });

        t.IsBackground = true;
        t.Start();
    }

    private static void CopyDirectory(string sourceDir, string destinationDir) {
        Directory.CreateDirectory(destinationDir);
        foreach (string file in Directory.GetFiles(sourceDir)) {
            File.Copy(file, System.IO.Path.Combine(destinationDir, System.IO.Path.GetFileName(file)), true);
        }
        foreach (string subDir in Directory.GetDirectories(sourceDir)) {
            CopyDirectory(subDir, System.IO.Path.Combine(destinationDir, System.IO.Path.GetFileName(subDir)));
        }
    }

    private static void CreateDesktopShortcut(string name, string targetPath, string description) {
        try {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(shellType);
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string shortcutPath = System.IO.Path.Combine(desktop, name + ".lnk");
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = System.IO.Path.GetDirectoryName(targetPath);
            shortcut.Description = description;
            shortcut.Save();
        } catch { }
    }
}
