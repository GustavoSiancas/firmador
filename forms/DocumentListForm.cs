using FirmadorPades.Models;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace FirmadorPades.Forms;

/// <summary>Lista de documentos y vista previa en la misma ventana.</summary>
internal sealed class DocumentListForm : Form
{
    private const string DocumentUrl = "https://documento.invalid/documento.pdf";
    private static readonly Color BrandTeal = Color.FromArgb(20, 125, 106);
    private static readonly Color Surface = Color.FromArgb(244, 247, 251);
    private readonly IReadOnlyList<TemporarySigningDocument> _documents;
    private readonly WebView2 _viewer = new() { Dock = DockStyle.Fill };
    private readonly Label _status = new()
    {
        Dock = DockStyle.Fill, Text = "Cargando documento…", TextAlign = ContentAlignment.MiddleCenter
    };
    private readonly Label _fileName = new()
    {
        Dock = DockStyle.Top, Height = 48, Padding = new Padding(16, 0, 16, 0),
        AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft,
        Font = new Font("Segoe UI", 11, FontStyle.Bold)
    };
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly List<Stream> _streams = [];
    private CoreWebView2? _core;
    private TemporarySigningDocument? _selectedDocument;

    public DocumentListForm(IReadOnlyList<TemporarySigningDocument> documents, Form owner)
    {
        _documents = documents;
        Text = "Documentos recibidos";
        StartPosition = FormStartPosition.Manual;
        Size = new Size(1080, 760);
        MinimizeBox = false;
        BackColor = Surface;
        Font = new Font("Segoe UI", 9);

        var area = Screen.FromControl(owner).WorkingArea;
        int x = Math.Min(owner.Right + 15, area.Right - Width);
        int y = Math.Max(area.Top, area.Top + (area.Height - Height) / 2);
        Location = new Point(Math.Max(area.Left, x), y);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(16)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var previewPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        previewPanel.Controls.Add(_viewer);
        previewPanel.Controls.Add(_status);
        previewPanel.Controls.Add(_fileName);

        _list.Font = new Font("Segoe UI", 10);
        _list.BackColor = Color.White;
        _list.BorderStyle = BorderStyle.FixedSingle;
        _list.DisplayMember = nameof(TemporarySigningDocument.FileName);
        foreach (var document in _documents)
            _list.Items.Add(document);
        _list.SelectedIndexChanged += (_, _) => SelectDocument();

        var documentsPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        documentsPanel.Controls.Add(_list);
        documentsPanel.Controls.Add(new Label
        {
            Text = "Documentos", Dock = DockStyle.Top, Height = 42,
            Padding = new Padding(12, 0, 0, 0), TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            BackColor = BrandTeal, ForeColor = Color.White
        });
        layout.Controls.Add(previewPanel, 0, 0);
        layout.Controls.Add(documentsPanel, 1, 0);
        Controls.Add(layout);

        var header = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = BrandTeal };
        header.Controls.Add(new Label
        {
            Text = "Firmador CAL", AutoSize = true, Location = new Point(22, 13),
            Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.White
        });
        header.Controls.Add(new Label
        {
            Text = $"{_documents.Count} documentos recibidos para firmar", AutoSize = true,
            Location = new Point(24, 45), Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(222, 244, 240)
        });
        Controls.Add(header);

        if (_list.Items.Count > 0)
            _list.SelectedIndex = 0;

        Shown += LoadPreview;
    }

    private void SelectDocument()
    {
        _selectedDocument = _list.SelectedItem as TemporarySigningDocument;
        _fileName.Text = _selectedDocument?.FileName ?? "Seleccione un documento";
        NavigateCurrentDocument();
    }

    private async void LoadPreview(object? sender, EventArgs e)
    {
        try
        {
            string profile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FirmadorCAL", "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: profile);
            if (IsDisposed || Disposing) return;

            await _viewer.EnsureCoreWebView2Async(environment);
            if (IsDisposed || Disposing) return;

            _core = _viewer.CoreWebView2;
            _core.Settings.AreDefaultContextMenusEnabled = false;
            _core.Settings.AreDevToolsEnabled = false;
            _core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            _core.Settings.HiddenPdfToolbarItems = CoreWebView2PdfToolbarItems.Save |
                CoreWebView2PdfToolbarItems.SaveAs | CoreWebView2PdfToolbarItems.Print;
            _core.NewWindowRequested += (_, args) => args.Handled = true;
            _core.DownloadStarting += (_, args) => args.Cancel = true;
            _core.NavigationStarting += (_, args) =>
                args.Cancel = !args.Uri.StartsWith(DocumentUrl, StringComparison.OrdinalIgnoreCase);
            _core.AddWebResourceRequestedFilter("https://documento.invalid/*", CoreWebView2WebResourceContext.All);
            _core.WebResourceRequested += ServeSelectedDocument;
            _core.NavigationCompleted += (_, args) =>
            {
                _status.Visible = !args.IsSuccess;
                if (!args.IsSuccess)
                    _status.Text = "No se pudo mostrar el PDF. Seleccione otro documento o vuelva a intentarlo.";
            };

            NavigateCurrentDocument();
        }
        catch (Exception ex)
        {
            if (IsDisposed || Disposing) return;
            _status.Text = ex is WebView2RuntimeNotFoundException
                ? "Instale Microsoft Edge WebView2 Runtime para visualizar el PDF."
                : $"No se pudo abrir la vista previa: {ex.Message}";
            _status.Visible = true;
        }
    }

    private void NavigateCurrentDocument()
    {
        if (_core is null || _selectedDocument is null)
            return;

        _status.Visible = true;
        _status.Text = "Cargando documento…";
        _core.Navigate($"{DocumentUrl}?id={Uri.EscapeDataString(_selectedDocument.Id)}");
    }

    private void ServeSelectedDocument(object? sender, CoreWebView2WebResourceRequestedEventArgs args)
    {
        if (_selectedDocument is null || _core is null)
            return;

        var stream = new MemoryStream(_selectedDocument.PreviewPdfBytes, writable: false);
        _streams.Add(stream);
        args.Response = _core.Environment.CreateWebResourceResponse(
            stream, 200, "OK", "Content-Type: application/pdf\r\nCache-Control: no-store");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _viewer.Dispose();
            foreach (var stream in _streams)
                stream.Dispose();
            _streams.Clear();
        }
        base.Dispose(disposing);
    }
}
