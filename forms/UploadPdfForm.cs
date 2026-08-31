using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Drawing;
using FirmadorPades.Services;

namespace FirmadorPades.Forms;

public partial class UploadPdfForm : Form
{
    private readonly PdfValidationService _validator = new();

    // Panel inicial
    private readonly Panel pnlSelect;
    private readonly Panel pnlDrop;

    private readonly ApiService _apiService;
    private readonly DownloadService _downloadService;
    private readonly string _documentId;
    private readonly Label lblDrop;
    private readonly Button btnBrowse;

    // Panel de vista previa
    private readonly Panel pnlPreview;
    private readonly Label lblFileName;
    private readonly WebView2 webView;
    private readonly DataGridView dgv;
    private readonly Button btnUpload;

    private readonly Button btnChangePdf;

    private string? _tempPdf;
    private byte[]? _pdfBytes;

    public byte[]? PdfBytes => _pdfBytes;

    public UploadPdfForm(
        string documentId,
        ApiService apiService,
        DownloadService downloadService)
    {
        _documentId = documentId;
        _apiService = apiService;   
        _downloadService = downloadService;

        Text = "Subir PDF Firmado";
        Icon = new Icon(Path.Combine(
                AppContext.BaseDirectory,
                "assets",
                "logo.ico"));
        Width = 1100;
        Height = 750;

        BackColor = Color.FromArgb(244, 247, 251);

        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        //
        // PANEL SELECCIÓN
        //
        pnlSelect = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(244, 247, 251)
        };

        var title = new Label
        {
            Text = "Subir documento ya firmado",
            AutoSize = true,
            Location = new Point(130, 55),
            Font = new Font("Segoe UI", 19, FontStyle.Bold),
            ForeColor = Color.Black
        };
        var subtitle = new Label
        {
            Text = "Seleccione un PDF con firma digital. Validaremos sus firmas antes de enviarlo.",
            AutoSize = true,
            Location = new Point(132, 92),
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.Black
        };

        pnlDrop = new Panel
        {
            Left = 130,
            Top = 145,
            Width = 820,
            Height = 220,

            BorderStyle = BorderStyle.FixedSingle,

            AllowDrop = true,

            BackColor = Color.White
        };

        lblDrop = new Label
        {
            Dock = DockStyle.Fill,

            Text =
                "Arrastre un PDF aquí\n\nó\n\nSeleccione un documento",

            TextAlign = ContentAlignment.MiddleCenter,

            Font = new Font(
                "Segoe UI",
                14,
                FontStyle.Bold)
        };

        pnlDrop.Controls.Add(lblDrop);

        btnBrowse = new Button
        {
            Text = "Elegir PDF",

            Width = 220,
            Height = 50,

            Left = 440,
            Top = 400,

            Font = new Font(
                "Segoe UI",
                11,
                FontStyle.Bold),

            BackColor = Color.FromArgb(22, 119, 160),
            ForeColor = Color.White,

            FlatStyle = FlatStyle.Flat,

            Cursor = Cursors.Hand
        };

        btnBrowse.FlatAppearance.BorderSize = 0;

        pnlSelect.Controls.Add(title);
        pnlSelect.Controls.Add(subtitle);
        pnlSelect.Controls.Add(pnlDrop);
        pnlSelect.Controls.Add(btnBrowse);

        //
        // PANEL PREVIEW
        //
        pnlPreview = new Panel
        {
            Dock = DockStyle.Fill,
            Visible = false,
            BackColor = Color.FromArgb(244, 247, 251)
        };

        lblFileName = new Label
        {
            Left = 25,
            Top = 18,
            Width = 1035,

            Font = new Font(
                "Segoe UI",
                11,
                FontStyle.Bold)
        };

        webView = new WebView2
        {
            Left = 25,
            Top = 55,
            Width = 665,
            Height = 555
        };

        dgv = new DataGridView
        {
            Left = 710,
            Top = 55,
            Width = 370,
            Height = 555,

            ReadOnly = true,

            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,

            RowHeadersVisible = false,

            AutoGenerateColumns = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            EnableHeadersVisualStyles = false,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(16, 44, 84), ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold) },
            DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.Black, Font = new Font("Segoe UI", 9), SelectionBackColor = Color.Gainsboro, SelectionForeColor = Color.Black }
        };

        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Campo", Width = 110 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Firmante", Width = 170 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Subject", Width = 240 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Integridad", Width = 80 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Revisión", Width = 70 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Cubre final", Width = 80 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Algoritmo", Width = 100 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Fecha", Width = 120 });

        btnUpload = new Button
        {
            Text = "Subir PDF",

            Width = 220,
            Height = 50,

            Left = 555,
            Top = 640,

            Enabled = false,

            Font = new Font(
                "Segoe UI",
                11,
                FontStyle.Bold),

            BackColor = Color.FromArgb(20, 125, 106),
            ForeColor = Color.White,

            FlatStyle = FlatStyle.Flat,

            Cursor = Cursors.Hand
        };

        btnUpload.FlatAppearance.BorderSize = 0;

        btnChangePdf = new Button
        {
            Text = "Cambiar PDF",

            Width = 220,
            Height = 50,

            Left = 325,
            Top = 640,

            Font = new Font(
                "Segoe UI",
                11,
                FontStyle.Bold),

            BackColor = Color.White,
            ForeColor = Color.Black,

            FlatStyle = FlatStyle.Flat,

            Cursor = Cursors.Hand
        };

        btnChangePdf.FlatAppearance.BorderSize = 0;

        btnChangePdf.Click += BtnChangePdf_Click;

        btnUpload.Click += BtnUpload_Click;

        

        pnlPreview.Controls.Add(lblFileName);
        pnlPreview.Controls.Add(webView);
        pnlPreview.Controls.Add(dgv);
        pnlPreview.Controls.Add(btnChangePdf);
        pnlPreview.Controls.Add(btnUpload);

        Controls.Add(pnlSelect);
        Controls.Add(pnlPreview);

        Load += UploadPdfForm_Load;

        btnBrowse.Click += BtnBrowse_Click;

        pnlDrop.DragEnter += PnlDrop_DragEnter;
        pnlDrop.DragDrop += PnlDrop_DragDrop;
    }
    private async void UploadPdfForm_Load(object? sender, EventArgs e)
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FirmaApp","WebView2");

        Directory.CreateDirectory(folder);

        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: folder);
        await webView.EnsureCoreWebView2Async(env);
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using OpenFileDialog ofd = new();
        ofd.Filter = "PDF (*.pdf)|*.pdf";

        if (ofd.ShowDialog() == DialogResult.OK)
            LoadPdf(ofd.FileName);
    }

    private void PnlDrop_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private void PnlDrop_DragDrop(object? sender, DragEventArgs e)
    {
        var files = (string[])e.Data!.GetData(DataFormats.FileDrop)!;

        if (files.Length > 0 && Path.GetExtension(files[0]).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            LoadPdf(files[0]);
    }

    private void LoadPdf(string path)
    {
        _pdfBytes = File.ReadAllBytes(path);

        if (_tempPdf != null && File.Exists(_tempPdf))
            File.Delete(_tempPdf);

        _tempPdf = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid()}.pdf");

        File.WriteAllBytes(_tempPdf, _pdfBytes);

        // Cambiar de pantalla
        pnlSelect.Visible = false;
        pnlPreview.Visible = true;

        // Nombre del documento
        lblFileName.Text = $"Documento: {Path.GetFileName(path)}";

        // Mostrar PDF
        webView.Source = new Uri(_tempPdf);

        dgv.Rows.Clear();

        var signatures = _validator.GetSignatures(_pdfBytes);

        dgv.Rows.Clear();

        if (!signatures.Any())
        {
            dgv.Rows.Add(
                "-",
                "El documento no contiene firmas",
                "-",
                "-",
                "-",
                "-",
                "-",
                "-");

            btnUpload.Enabled = false;
            return;
        }

        foreach (var s in signatures)
        {
            dgv.Rows.Add(
                s.SignatureName,
                s.Signer,
                s.Subject,
                s.IsValid ? "Válida" : "Inválida",
                $"{s.Revision}/{s.TotalRevisions}",
                s.CoversWholeDocument ? "Sí" : "No",
                s.Algorithm,
                s.SigningDate?.ToString("dd/MM/yyyy HH:mm"));
        }

        btnUpload.Enabled = signatures.All(x => x.IsValid);
    }

    private void BtnChangePdf_Click(
        object? sender,
        EventArgs e)
    {
        using OpenFileDialog ofd = new();

        ofd.Filter = "PDF (*.pdf)|*.pdf";

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            LoadPdf(ofd.FileName);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);

        if(_tempPdf != null && File.Exists(_tempPdf))
            File.Delete(_tempPdf);
    }

    private async void BtnUpload_Click(
        object? sender,
        EventArgs e)
    {
        if (_pdfBytes == null)
            return;

        try
        {
            Cursor = Cursors.WaitCursor;

            btnUpload.Enabled = false;
            btnChangePdf.Enabled = false;

            await _apiService.UpdateDocumentAsync(_pdfBytes);

            using var preview = new SignedPdfForm(_pdfBytes, _downloadService);

            preview.ShowDialog(this);

            if (preview.CloseApplicationRequested)
            {
                BeginInvoke(Application.Exit);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Error al subir el documento",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;

            btnUpload.Enabled = true;
            btnChangePdf.Enabled = true;
        }
    }
}
