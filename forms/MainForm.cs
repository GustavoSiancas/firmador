using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using FirmadorPades.Services;

namespace FirmadorPades.Forms;

public partial class MainForm : Form
{
    private readonly string _documentId;
    private readonly byte[] _pdfBytes;
    private readonly ApiService _apiService;
    private readonly DownloadService _downloadService;

    private readonly CertificateService _certificateService;
    private readonly OrchestratorService _orchestratorService;
    private readonly StampService _stampService;

    private readonly Label lblDocumentId;
    private readonly WebView2 webViewPdf;
    private readonly Panel panelPdf;

    private readonly Button btnSign;
    private readonly Button btnDownload;
    private readonly Button btnUpload;

    private string? _tempPdfFile;

    public MainForm(
        string documentId,
        ApiService apiService,
        byte[] pdfBytes,
        CertificateService certificateService,
        OrchestratorService orchestratorService,
        StampService stampService,
        DownloadService downloadService)
    {
        _documentId = documentId;
        _pdfBytes = pdfBytes;
        _apiService = apiService;

        _certificateService = certificateService;
        _orchestratorService = orchestratorService;
        _stampService = stampService;
        _downloadService = downloadService;

        Text = "FIRMADOR CAL 2D";
        Icon = new Icon(Path.Combine(
                AppContext.BaseDirectory,
                "assets",
                "logo.ico"));

        Width = 1060;
        Height = 800;

        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        BackColor = Color.FromArgb(244, 247, 251);

        var header = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Color.FromArgb(16, 44, 84) };
        header.Controls.Add(new Label { AutoSize = true, Text = "FIRMADOR CAL 2D", Font = new Font("Segoe UI", 17, FontStyle.Bold), ForeColor = Color.White, Location = new Point(30, 16) });

        lblDocumentId = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            ForeColor = Color.FromArgb(214, 226, 242),
            Location = new Point(32, 51),
            Text = $"Documento #{_documentId}  ·  listo para firmar"
        };

        panelPdf = new Panel
        {
            Location = new Point(30, 105),
            Size = new Size(980, 550),
            BorderStyle = BorderStyle.None,
            BackColor = Color.White
        };

        webViewPdf = new WebView2
        {
            Dock = DockStyle.Fill
        };

        panelPdf.Controls.Add(webViewPdf);

        btnSign = new Button
        {
            Text = "Firmar PDF",
            Width = 190,
            Height = 48,
            Location = new Point(820, 680),

            Font = new Font("Segoe UI", 11, FontStyle.Bold),

            BackColor = Color.FromArgb(22, 119, 160),
            ForeColor = Color.White,

            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };

        btnSign.FlatAppearance.BorderSize = 0;
        btnSign.Click += BtnSign_Click;

        btnDownload = new Button
        {
            Text = "⬇ Descargar PDF",
            Width = 190,
            Height = 48,
            Location = new Point(30, 680),

            Font = new Font("Segoe UI", 11, FontStyle.Bold),

            BackColor = Color.FromArgb(24, 24, 27),
            ForeColor = Color.White,

            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,

            Visible = false
        };

        btnDownload.FlatAppearance.BorderSize = 0;
        btnDownload.Click += BtnDownload_Click;

        btnUpload = new Button
        {
            Text = "Subir PDF Firmado",
            Width = 190,
            Height = 48,
            Location = new Point(820, 680),

            Font = new Font("Segoe UI", 11, FontStyle.Bold),

            BackColor = Color.FromArgb(20, 125, 106),
            ForeColor = Color.White,

            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,

            Visible = false
        };

        btnUpload.FlatAppearance.BorderSize = 0;
        btnUpload.Click += BtnUpload_Click;

        Controls.Add(header);
        header.Controls.Add(lblDocumentId);
        Controls.Add(panelPdf);
        Controls.Add(btnSign);
        Controls.Add(btnDownload);
        Controls.Add(btnUpload);

        Load += MainForm_Load;
    }

    private async void MainForm_Load(
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

    private void BtnSign_Click(
        object? sender,
        EventArgs e)
    {
        bool? useDefault = SignaturePreviewForm.AskPlacementMode(this);
        if (useDefault is null)
            return;

        using var preview = new SignaturePreviewForm(
            _pdfBytes,
            _stampService.CreatePreviewStamp(),
            startInSelectionMode: !useDefault.Value);
        if (preview.ShowDialog(this) != DialogResult.OK || preview.SignatureLocation is null)
            return;

        using var form = new CertificateForm(
            _certificateService,
            _orchestratorService,
            _documentId,
            _pdfBytes,
            preview.SignatureLocation,
            _downloadService);

        form.ShowDialog(this);

        // Cuando la firma termine correctamente,
        // simplemente habilitamos el botón.
        btnUpload.Enabled = true;
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

    private void BtnUpload_Click(
        object? sender,
        EventArgs e)
    {
        using var form = new UploadPdfForm(
        _documentId,
        _apiService,
        _downloadService);

        if (form.ShowDialog(this) == DialogResult.OK)
        {
            // Aquí luego puedes obtener el PDF
            byte[]? pdf = form.PdfBytes;

            if (pdf != null)
            {
                // Aquí llamarás a tu API
                //_apiService.UpdateDocument(_documentId, pdf);
            }
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
    }
}
