using System.Text.Json;
using FirmadorPades.Models;
using FirmadorPades.Services;
using Microsoft.Web.WebView2.Core;

namespace FirmadorPades.Forms;

public partial class SignaturePreviewForm : Form
{
    private readonly SignaturePreviewService _previewService;
    private bool _selectionEnabled;
    private bool _ready;

    public SignatureLocation? SignatureLocation { get; private set; }

    public SignaturePreviewForm(byte[] pdfBytes, byte[] previewStampBytes, bool startInSelectionMode)
    {
        InitializeComponent();
        _previewService = new SignaturePreviewService(pdfBytes, previewStampBytes);
        _selectionEnabled = startInSelectionMode;
        nudPage.Maximum = _previewService.PageCount;
        lblStatus.Text = startInSelectionMode ? "Selección activa: haga clic sobre el PDF." : "Posición predeterminada: página 1.";
        btnAccept.Enabled = true;
        Load += SignaturePreviewForm_Load;
        btnSelectLocation.Click += async (_, _) => await SetSelectionAsync(true);
        btnDisableSelection.Click += async (_, _) => await SetSelectionAsync(false);
        btnZoomIn.Click += async (_, _) => { if (_ready) await _previewService.ChangeZoomAsync(pdfViewer, true); };
        btnZoomOut.Click += async (_, _) => { if (_ready) await _previewService.ChangeZoomAsync(pdfViewer, false); };
        btnAccept.Click += (_, _) => { SignatureLocation = _previewService.Location; DialogResult = DialogResult.OK; Close(); };
        btnCancel.Click += (_, _) => Close();
        nudPage.ValueChanged += async (_, _) => { if (_ready) await _previewService.SetActivePageAsync(pdfViewer, (int)nudPage.Value); };
        FormClosed += (_, _) => _previewService.Dispose();
        Icon = new Icon(Path.Combine(
            AppContext.BaseDirectory,
            "assets",
            "logo.ico"));
        Width = 1100;
        Height = 750;
    }

    public static bool? AskPlacementMode(IWin32Window owner)
    {
        using var dialog = new PlacementChoiceDialog();
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.UseDefault : null;
    }

    private async void SignaturePreviewForm_Load(object? sender, EventArgs e)
    {
        try
        {
            string dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FirmaApp", "WebView2");
            Directory.CreateDirectory(dataFolder);
            await pdfViewer.EnsureCoreWebView2Async(await CoreWebView2Environment.CreateAsync(userDataFolder: dataFolder));
            pdfViewer.CoreWebView2.WebMessageReceived += async (_, args) =>
            {
                var click = JsonSerializer.Deserialize<PreviewClick>(args.TryGetWebMessageAsString());
                if (click is null) return;
                if (!string.IsNullOrWhiteSpace(click.error))
                {
                    MessageBox.Show(click.error, "Error al renderizar el PDF", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (!_selectionEnabled) return;
                SignatureLocation location = _previewService.SetLocationFromScreen(click.page, click.x, click.y);
                if (nudPage.Value != location.Page) nudPage.Value = location.Page;
                lblStatus.Text = $"Ubicación: página {location.Page}, X={location.X:0}, Y={location.Y:0}";
                await _previewService.RefreshOverlayAsync(pdfViewer);
            };
            await _previewService.LoadAsync(pdfViewer, _selectionEnabled);
            _ready = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "No se pudo abrir la previsualización", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }

    private async Task SetSelectionAsync(bool enabled)
    {
        _selectionEnabled = enabled;
        lblStatus.Text = enabled ? "Selección activa: haga clic sobre el PDF." : "Selección desactivada: puede navegar libremente.";
        if (_ready) await _previewService.SetSelectionModeAsync(pdfViewer, enabled);
    }

    private sealed class PreviewClick { public int page { get; set; } public float x { get; set; } public float y { get; set; } public string? error { get; set; } }

    private sealed class PlacementChoiceDialog : Form
    {
        public bool UseDefault { get; private set; }
        public PlacementChoiceDialog()
        {
            Text = "Ubicación de firma";
            ClientSize = new Size(410, 145);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            var label = new Label { Text = "¿Cómo desea colocar la firma?", AutoSize = true, Location = new Point(22, 20) };
            var predefined = new Button { Text = "Usar posición predeterminada", AutoSize = true, Location = new Point(22, 62) };
            var choose = new Button { Text = "Elegir posición", AutoSize = true, Location = new Point(230, 62) };
            predefined.Click += (_, _) => { UseDefault = true; DialogResult = DialogResult.OK; Close(); };
            choose.Click += (_, _) => { UseDefault = false; DialogResult = DialogResult.OK; Close(); };
            Controls.AddRange([label, predefined, choose]);
        }
    }
}
