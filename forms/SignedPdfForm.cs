using System.Drawing;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using FirmadorPades.Services;

namespace FirmadorPades.Forms;

public class SignedPdfForm : Form
{
    private readonly byte[] _pdfBytes;
    private readonly DownloadService _downloadService;

    private readonly Label lblTitle;
    private readonly WebView2 webViewPdf;
    private readonly Button btnClose;
    private readonly Button btnDownload;

    private string? _tempPdfFile;

    public bool CloseApplicationRequested { get; private set; }

    public SignedPdfForm(byte[] pdfBytes, DownloadService downloadService)
    {
        _pdfBytes = pdfBytes;
        _downloadService = downloadService;

        Text = "Documento Firmado";
        Icon = new Icon(Path.Combine(
            AppContext.BaseDirectory,
            "assets",
            "logo.ico"));

        Width = 1060;
        Height = 800;

        StartPosition = FormStartPosition.CenterParent;

        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.FromArgb(244, 247, 251);

        var header = new Panel { Dock = DockStyle.Top, Height = 94, BackColor = Color.FromArgb(20, 125, 106) };

        lblTitle = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            ForeColor = Color.White,
            Text = "Documento firmado correctamente",
            Location = new Point(30, 18)
        };

        webViewPdf = new WebView2
        {
            Location = new Point(30, 118),
            Size = new Size(980, 555),
            Anchor = AnchorStyles.Top
                   | AnchorStyles.Bottom
                   | AnchorStyles.Left
                   | AnchorStyles.Right
        };

        btnClose = new Button
        {
            Text = "Cerrar",
            Width = 180,
            Height = 45,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Location = new Point(540, 690),
            BackColor = Color.FromArgb(16, 44, 84),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        btnDownload = new Button
        {
            Text = "Descargar PDF",
            Width = 180,
            Height = 45,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Location = new Point(340, 690),
            BackColor = Color.FromArgb(22, 119, 160),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };

        btnClose.Click += BtnClose_Click;
        btnDownload.Click += BtnDownload_Click;

        header.Controls.Add(lblTitle);
        Controls.Add(header);
        Controls.Add(webViewPdf);
        Controls.Add(btnDownload);
        Controls.Add(btnClose);

        Load += SignedPdfForm_Load;
    }

    private async void SignedPdfForm_Load(
        object? sender,
        EventArgs e)
    {
        try
        {
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FirmaApp",
                "WebView2");

            Directory.CreateDirectory(userDataFolder);

            var environment = await CoreWebView2Environment.CreateAsync(
                userDataFolder: userDataFolder);

            await webViewPdf.EnsureCoreWebView2Async(environment);

            _tempPdfFile = Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid()}.pdf");

            await File.WriteAllBytesAsync(
                _tempPdfFile,
                _pdfBytes);

            webViewPdf.Source = new Uri(_tempPdfFile);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void BtnClose_Click(
        object? sender,
        EventArgs e)
    {
        CloseApplicationRequested = true;
        Close();
    }

    private async void BtnDownload_Click(
        object? sender,
        EventArgs e)
    {
        try
        {
            btnDownload.Enabled = false;
            await _downloadService.DownloadPdfAsync(_pdfBytes);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Error al descargar",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            btnDownload.Enabled = true;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);

        try
        {
            if (!string.IsNullOrWhiteSpace(_tempPdfFile) &&
                File.Exists(_tempPdfFile))
            {
                File.Delete(_tempPdfFile);
            }
        }
        catch
        {
        }

        // Esta es la pantalla final del flujo: el PDF ya fue firmado y subido.
        // Al cerrarla no debemos regresar a CertificateForm ni a MainForm.
        Application.Exit();
    }
}
