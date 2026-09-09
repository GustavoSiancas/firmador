using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace FirmadorPades.Forms;

internal sealed class DocumentPreviewForm : Form
{
    private readonly WebView2 _viewer = new() { Dock = DockStyle.Fill };
    private readonly Label _status = new() { Dock = DockStyle.Fill, Text = "Cargando documento…", TextAlign = ContentAlignment.MiddleCenter };
    private readonly byte[] _pdf;
    private readonly List<Stream> _streams = new();
    private const string DocumentUrl = "https://documento.invalid/documento.pdf";

    public DocumentPreviewForm(byte[] pdf, string fileName, Form owner)
    {
        _pdf = pdf;

        Text = "Vista previa del documento";
        StartPosition = FormStartPosition.Manual;

        Size = new Size(620, 760);

        var area = Screen.FromControl(owner).WorkingArea;

        // Siempre a la derecha del formulario principal
        int x = owner.Right + 5;

        // Centrado verticalmente en la pantalla
        int y = area.Top + (area.Height / 2) - (Height / 2);

        // Evitar que salga por arriba o abajo
        y = Math.Max(
            area.Top,
            Math.Min(y, area.Bottom - Height)
        );

        Location = new Point(x, y);

        ShowInTaskbar = false;
        BackColor = Color.FromArgb(244, 247, 251);

        var name = new Label
        {
            Text = fileName,
            Dock = DockStyle.Top,
            Height = 56,
            Padding = new Padding(16, 0, 16, 0),
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 11, FontStyle.Bold)
        };

        Controls.Add(_viewer);
        Controls.Add(_status);
        Controls.Add(name);

        _status.BringToFront();

        Shown += LoadPreview;
    }

    private async void LoadPreview(object? sender, EventArgs e)
    {
        try
        {
            string profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FirmadorCAL", "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: profile);
            if (IsDisposed || Disposing) return;
            await _viewer.EnsureCoreWebView2Async(environment);
            if (IsDisposed || Disposing) return;
            var core = _viewer.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.HiddenPdfToolbarItems = CoreWebView2PdfToolbarItems.Save |
                CoreWebView2PdfToolbarItems.SaveAs | CoreWebView2PdfToolbarItems.Print;
            core.NewWindowRequested += (_, args) => args.Handled = true;
            core.DownloadStarting += (_, args) => args.Cancel = true;
            core.NavigationStarting += (_, args) =>
                args.Cancel = args.Uri.Split('#')[0] != DocumentUrl;
            core.AddWebResourceRequestedFilter("https://documento.invalid/*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, args) =>
            {
                var stream = new MemoryStream(_pdf, writable: false);
                _streams.Add(stream);
                args.Response = environment.CreateWebResourceResponse(stream, 200, "OK",
                    "Content-Type: application/pdf\r\nCache-Control: no-store");
            };
            core.NavigationCompleted += (_, args) =>
            {
                _status.Visible = !args.IsSuccess;
                if (!args.IsSuccess) _status.Text = "No se pudo mostrar el PDF. Cierre esta ventana y vuelva a intentarlo.";
            };
            core.Navigate(DocumentUrl);
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _viewer.Dispose();
            foreach (var stream in _streams) stream.Dispose();
            _streams.Clear();
        }
        base.Dispose(disposing);
    }
}
